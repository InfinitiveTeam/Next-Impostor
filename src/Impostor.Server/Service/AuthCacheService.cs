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
/// 认证缓存服务 - 存储用户认证信息。
///
/// 主要索引：matchmakerToken（服务端签发、客户端在握手中发回）
/// 次要索引：IP 地址（精确匹配，不猜测）
/// 辅助索引：FriendCode（用于 DTLS 模式）
///
/// 注意：已移除"使用最近认证玩家"的猜测逻辑，该逻辑是 PUID 混乱的根源。
/// </summary>
public static class AuthCacheService
{
    // 主要缓存：matchmakerToken -> UserAuthInfo
    private static readonly ConcurrentDictionary<string, UserAuthInfo> _tokenCache = new();

    // 辅助缓存：IP -> matchmakerToken（精确 IP 匹配，不猜测）
    private static readonly ConcurrentDictionary<string, string> _ipToTokenMapping = new();

    // 辅助缓存：FriendCode -> matchmakerToken（用于 DTLS 模式）
    private static readonly ConcurrentDictionary<string, string> _friendCodeToTokenMapping = new();

    private static readonly ILogger _logger = LoggerFactory
        .Create(b => b.AddConsole())
        .CreateLogger("AuthCacheService");

    private static readonly TimeSpan AuthEntryTtl = TimeSpan.FromMinutes(10);

    /// <summary>
    /// 添加用户认证信息到缓存。
    /// authToken 参数应为服务端签发的 matchmakerToken（SHA256 哈希，base64）。
    /// </summary>
    public static void AddUserAuth(
        string productUserId,
        string authToken,          // matchmakerToken（服务端签发）
        string? eosToken = null,   // 原始 EOS JWT token（可选，仅用于后端 API 调用）
        string? friendCode = null,
        IPAddress? clientIp = null,
        string? playerName = null,
        int? clientVersion = null)
    {
        var info = new UserAuthInfo
        {
            ProductUserId = productUserId,
            AuthToken = authToken,
            EosToken = eosToken,
            FriendCode = friendCode ?? string.Empty,
            Timestamp = DateTime.UtcNow,
            ClientIp = clientIp?.ToString(),
            PlayerName = playerName,
        };

        // 主索引：matchmakerToken
        _tokenCache[authToken] = info;

        // 辅助索引：精确 IP
        if (clientIp != null)
        {
            var ipKey = clientIp.ToString();
            _ipToTokenMapping[ipKey] = authToken;

            // IPv6 mapped IPv4 支持
            if (clientIp.IsIPv4MappedToIPv6)
            {
                _ipToTokenMapping[clientIp.MapToIPv4().ToString()] = authToken;
            }
        }

        // 辅助索引：FriendCode
        if (!string.IsNullOrEmpty(friendCode))
        {
            _friendCodeToTokenMapping[friendCode] = authToken;
        }

        _logger.LogDebug(
            "Auth cached: PUID={Puid}, FriendCode={FriendCode}, IP={Ip}",
            productUserId, friendCode ?? "(none)", clientIp?.ToString() ?? "(none)");

        ScheduleCleanup();
    }

    /// <summary>
    /// 通过 matchmakerToken 获取用户认证信息（主要查找方式）。
    /// </summary>
    public static UserAuthInfo? GetUserAuthByToken(string matchmakerToken)
    {
        if (_tokenCache.TryGetValue(matchmakerToken, out var info))
        {
            if (IsExpired(info))
            {
                _tokenCache.TryRemove(matchmakerToken, out _);
                return null;
            }
            return info;
        }
        return null;
    }

    /// <summary>
    /// 通过 IP 地址获取用户认证信息（精确匹配，不猜测）。
    /// </summary>
    public static UserAuthInfo? GetUserAuthByIp(IPAddress clientIp)
    {
        if (clientIp == null) return null;

        var ipKey = clientIp.ToString();
        if (_ipToTokenMapping.TryGetValue(ipKey, out var token))
        {
            return GetUserAuthByToken(token);
        }

        // IPv6 mapped IPv4 尝试
        if (clientIp.IsIPv4MappedToIPv6)
        {
            var ipv4Key = clientIp.MapToIPv4().ToString();
            if (_ipToTokenMapping.TryGetValue(ipv4Key, out token))
            {
                return GetUserAuthByToken(token);
            }
        }

        return null;
    }

    /// <summary>
    /// 通过 FriendCode 获取用户认证信息（用于 DTLS 握手模式）。
    /// </summary>
    public static UserAuthInfo? GetUserAuthByFriendCode(string friendCode)
    {
        if (_friendCodeToTokenMapping.TryGetValue(friendCode, out var token))
        {
            return GetUserAuthByToken(token);
        }
        return null;
    }

    /// <summary>
    /// 通过 PUID 获取用户认证信息。
    /// </summary>
    public static UserAuthInfo? GetUserAuthByPuid(string productUserId)
    {
        return _tokenCache.Values
            .FirstOrDefault(x => x.ProductUserId == productUserId && !IsExpired(x));
    }

    /// <summary>
    /// 获取缓存统计信息（用于调试）。
    /// </summary>
    public static (int TokenCount, int IpMappingCount) GetCacheStats()
    {
        return (_tokenCache.Count, _ipToTokenMapping.Count);
    }

    private static bool IsExpired(UserAuthInfo info)
    {
        return DateTime.UtcNow - info.Timestamp > AuthEntryTtl;
    }

    private static async void ScheduleCleanup()
    {
        await Task.Delay(TimeSpan.FromMinutes(5));
        CleanupExpiredCache();
    }

    private static void CleanupExpiredCache()
    {
        var expiredTokens = _tokenCache
            .Where(kv => IsExpired(kv.Value))
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in expiredTokens)
        {
            _tokenCache.TryRemove(key, out _);
        }

        // 清理孤立的 IP 映射
        var validTokens = new HashSet<string>(_tokenCache.Keys);
        var expiredIpKeys = _ipToTokenMapping
            .Where(kv => !validTokens.Contains(kv.Value))
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in expiredIpKeys)
        {
            _ipToTokenMapping.TryRemove(key, out _);
        }

        var expiredFcKeys = _friendCodeToTokenMapping
            .Where(kv => !validTokens.Contains(kv.Value))
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in expiredFcKeys)
        {
            _friendCodeToTokenMapping.TryRemove(key, out _);
        }

        if (expiredTokens.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} expired auth entries", expiredTokens.Count);
        }
    }
}

public class UserAuthInfo
{
    public string ProductUserId { get; set; } = string.Empty;

    /// <summary>服务端签发的 matchmakerToken（SHA256 base64）</summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>原始 EOS JWT token（用于调用 Innersloth 后端 API）</summary>
    public string? EosToken { get; set; }

    /// <summary>玩家的 FriendCode（格式：Name#XXXX）</summary>
    public string FriendCode { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string? ClientIp { get; set; }

    public string? PlayerName { get; set; }
}
