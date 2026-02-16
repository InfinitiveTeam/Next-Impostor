using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Impostor.Api.Innersloth;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Service;

/// <summary>
/// 认证缓存服务 - 存储用户认证信息
/// </summary>
public static class AuthCacheService
{
    private static readonly ConcurrentDictionary<string, UserAuthInfo> _authCache = new();
    private static readonly ConcurrentDictionary<string, string> _ipToPuidMapping = new();
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<HandshakeAuthCandidate>> _handshakeAuthCandidates = new();
    private static readonly ILogger _logger = CreateLogger();
    private static readonly TimeSpan AuthEntryTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan HandshakeCandidateTtl = TimeSpan.FromMinutes(2);

    private static ILogger CreateLogger()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });
        return loggerFactory.CreateLogger("AuthCacheService");
    }

    /// <summary>
    /// 添加用户认证信息到缓存
    /// </summary>
    public static void AddUserAuth(string productUserId, string authToken, IPAddress clientIp = null, string? playerName = null, int? clientVersion = null)
    {
        // 主要缓存：按PUID存储
        _authCache[productUserId] = new UserAuthInfo
        {
            ProductUserId = productUserId,
            AuthToken = authToken,
            Timestamp = DateTime.UtcNow,
            ClientIp = clientIp?.ToString()
        };

        // IP映射缓存（可选，用于IPv4/IPv6映射）
        if (clientIp != null)
        {
            var ipKey = GetIpKey(clientIp);
            _ipToPuidMapping[ipKey] = productUserId;

            // 如果是IPv6，也记录可能的IPv4映射
            if (clientIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                TryAddIPv4Mapping(clientIp, productUserId);
            }
        }

        if (!string.IsNullOrWhiteSpace(playerName) && clientVersion.HasValue)
        {
            var handshakeKey = GetHandshakeKey(playerName, clientVersion.Value);
            var queue = _handshakeAuthCandidates.GetOrAdd(handshakeKey, _ => new ConcurrentQueue<HandshakeAuthCandidate>());
            queue.Enqueue(new HandshakeAuthCandidate
            {
                ProductUserId = productUserId,
                ClientIp = clientIp,
                Timestamp = DateTime.UtcNow,
            });
        }

        _logger.LogDebug("User auth cached: PUID={ProductUserId}, IP={ClientIp}",
            productUserId, clientIp?.ToString() ?? "unknown");

        // 5分钟后清理过期缓存
        ScheduleCleanup();
    }

    /// <summary>
    /// 通过PUID获取用户认证信息
    /// </summary>
    public static UserAuthInfo? GetUserAuthByPuid(string productUserId)
    {
        if (_authCache.TryGetValue(productUserId, out var userAuthInfo))
        {
            if (DateTime.UtcNow - userAuthInfo.Timestamp > AuthEntryTtl)
            {
                _authCache.TryRemove(productUserId, out _);
                return null;
            }
            return userAuthInfo;
        }
        return null;
    }

    /// <summary>
    /// 通过IP地址获取用户认证信息
    /// </summary>
    public static UserAuthInfo? GetUserAuthByIp(IPAddress clientIp)
    {
        if (clientIp == null) return null;

        // 尝试直接通过IP查找
        var ipKey = GetIpKey(clientIp);
        if (_ipToPuidMapping.TryGetValue(ipKey, out var puid))
        {
            return GetUserAuthByPuid(puid);
        }

        // 如果是IPv6，尝试查找对应的IPv4映射
        if (clientIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            var ipv4Key = GetIPv4MappingKey(clientIp);
            if (ipv4Key != null && _ipToPuidMapping.TryGetValue(ipv4Key, out puid))
            {
                return GetUserAuthByPuid(puid);
            }
        }

        return null;
    }

    /// <summary>
    /// 尝试通过Token获取用户（用于TCP连接）
    /// </summary>
    public static UserAuthInfo? GetUserAuthByToken(string authToken)
    {
        foreach (var kvp in _authCache)
        {
            if (kvp.Value.AuthToken == authToken)
            {
                if (DateTime.UtcNow - kvp.Value.Timestamp > TimeSpan.FromMinutes(5))
                {
                    _authCache.TryRemove(kvp.Key, out _);
                    return null;
                }
                return kvp.Value;
            }
        }
        return null;
    }

    /// <summary>
    /// 从连接参数推断PUID
    /// </summary>
    public static string? InferPuidFromConnection(IPAddress clientIp, string clientName, GameVersion gameVersion)
    {
        // 0. 优先通过握手信息（昵称+版本）匹配，避免IPv4/IPv6不一致导致的误判。
        var authByHandshake = TryConsumeHandshakeAuth(clientName, gameVersion.Value, clientIp);
        if (authByHandshake != null)
        {
            _logger.LogDebug("Found PUID by handshake: {Puid} for {Name}", authByHandshake.ProductUserId, clientName);
            return authByHandshake.ProductUserId;
        }

        // 1. 首先尝试IP映射
        var authByIp = GetUserAuthByIp(clientIp);
        if (authByIp != null)
        {
            _logger.LogDebug("Found PUID by IP: {Puid} for {Name}",
                authByIp.ProductUserId, clientName);
            return authByIp.ProductUserId;
        }

        // 2. 如果没有找到，可以尝试基于最近认证的玩家（最后认证的玩家）
        var recentAuth = _authCache.Values
            .Where(x => DateTime.UtcNow - x.Timestamp < TimeSpan.FromMinutes(1))
            .OrderByDescending(x => x.Timestamp)
            .FirstOrDefault();

        if (recentAuth != null)
        {
            _logger.LogDebug("Using recent auth for {Name}: {Puid}",
                clientName, recentAuth.ProductUserId);
            return recentAuth.ProductUserId;
        }

        _logger.LogWarning("Could not infer PUID for {Name} from IP {Ip}",
            clientName, clientIp);
        return null;
    }

    private static string GetIpKey(IPAddress ip)
    {
        return ip.ToString();
    }

    private static string GetHandshakeKey(string name, int clientVersion)
    {
        return $"{name.Trim().ToLowerInvariant()}|{clientVersion}";
    }

    private static UserAuthInfo? TryConsumeHandshakeAuth(string clientName, int clientVersion, IPAddress? udpIp)
    {
        var key = GetHandshakeKey(clientName, clientVersion);
        if (!_handshakeAuthCandidates.TryGetValue(key, out var queue))
        {
            return null;
        }

        while (queue.TryPeek(out var candidate))
        {
            if (DateTime.UtcNow - candidate.Timestamp > HandshakeCandidateTtl)
            {
                queue.TryDequeue(out _);
                continue;
            }

            queue.TryDequeue(out _);
            if (udpIp != null && candidate.ClientIp != null && udpIp.Equals(candidate.ClientIp))
            {
                _logger.LogDebug("Matched handshake candidate with exact IP for {Name}", clientName);
            }

            return GetUserAuthByPuid(candidate.ProductUserId);
        }

        _handshakeAuthCandidates.TryRemove(key, out _);
        return null;
    }
    private static void TryAddIPv4Mapping(IPAddress ipv6Address, string productUserId)
    {
        try
        {
            // 尝试从IPv6地址提取可能的IPv4映射
            if (ipv6Address.IsIPv4MappedToIPv6)
            {
                var ipv4 = ipv6Address.MapToIPv4();
                var ipv4Key = GetIpKey(ipv4);
                _ipToPuidMapping[ipv4Key] = productUserId;
                _logger.LogDebug("Added IPv4 mapping: {IPv4} -> {Puid}", ipv4, productUserId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to add IPv4 mapping");
        }
    }

    private static string? GetIPv4MappingKey(IPAddress ipv6Address)
    {
        try
        {
            if (ipv6Address.IsIPv4MappedToIPv6)
            {
                var ipv4 = ipv6Address.MapToIPv4();
                return GetIpKey(ipv4);
            }
        }
        catch { }
        return null;
    }

    private static async void ScheduleCleanup()
    {
        await Task.Delay(TimeSpan.FromMinutes(5));
        CleanupExpiredCache();
    }

    /// <summary>
    /// 清理过期缓存（超过5分钟）
    /// </summary>
    private static void CleanupExpiredCache()
    {
        var now = DateTime.UtcNow;
        var expiredPuid = new List<string>();
        var expiredIpKeys = new List<string>();

        // 清理过期PUID
        foreach (var kvp in _authCache)
        {
            if (now - kvp.Value.Timestamp > AuthEntryTtl)
            {
                expiredPuid.Add(kvp.Key);
            }
        }

        foreach (var kvp in _handshakeAuthCandidates)
        {
            var queue = kvp.Value;
            while (queue.TryPeek(out var candidate) && now - candidate.Timestamp > HandshakeCandidateTtl)
            {
                queue.TryDequeue(out _);
            }

            if (queue.IsEmpty)
            {
                _handshakeAuthCandidates.TryRemove(kvp.Key, out _);
            }
        }

        // 清理过期IP映射
        foreach (var ipKey in _ipToPuidMapping.Keys)
        {
            if (_ipToPuidMapping.TryGetValue(ipKey, out var puid) && expiredPuid.Contains(puid))
            {
                expiredIpKeys.Add(ipKey);
            }
        }

        foreach (var key in expiredPuid)
        {
            _authCache.TryRemove(key, out _);
        }

        foreach (var key in expiredIpKeys)
        {
            _ipToPuidMapping.TryRemove(key, out _);
        }

        if (expiredPuid.Count > 0 || expiredIpKeys.Count > 0)
        {
            _logger.LogDebug("Cleaned up expired cache: {PuidCount} PUIDs, {IpCount} IP mappings",
                expiredPuid.Count, expiredIpKeys.Count);
        }
    }

    /// <summary>
    /// 获取缓存统计信息（用于调试）
    /// </summary>
    public static (int PuidCount, int IpMappingCount) GetCacheStats()
    {
        return (_authCache.Count, _ipToPuidMapping.Count);
    }
}

internal class HandshakeAuthCandidate
{
    public string ProductUserId { get; set; } = string.Empty;

    public IPAddress? ClientIp { get; set; }

    public DateTime Timestamp { get; set; }
}

public class UserAuthInfo
{
    public string ProductUserId { get; set; } = string.Empty;

    public string AuthToken { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string? ClientIp { get; set; }
}
