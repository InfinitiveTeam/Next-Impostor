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
/// 索引优先级（按可靠性排序）：
///   1. Nonce（uint32）：DTLS 认证端口生成，通过 LastNonce 字段回传，完全不依赖 IP
///   2. matchmakerToken（string）：HTTP /api/user 返回，部分客户端在握手中携带
///   3. IP 地址：兜底精确匹配
/// </summary>
public static class AuthCacheService
{
    // 主要缓存：authToken -> UserAuthInfo
    private static readonly ConcurrentDictionary<string, UserAuthInfo> _tokenCache = new();
    // Nonce -> authToken（IP 无关的最可靠匹配）
    private static readonly ConcurrentDictionary<uint, string> _nonceToToken = new();
    // IP -> authToken（兜底匹配）
    private static readonly ConcurrentDictionary<string, string> _ipToTokenMapping = new();
    // FriendCode -> authToken（辅助）
    private static readonly ConcurrentDictionary<string, string> _friendCodeToTokenMapping = new();

    private static readonly ILogger _logger = LoggerFactory
        .Create(b => b.AddConsole())
        .CreateLogger("AuthCacheService");

    private static readonly TimeSpan AuthEntryTtl = TimeSpan.FromMinutes(10);

    public static void AddUserAuth(
        string productUserId,
        string authToken,
        string? eosToken = null,
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

        _tokenCache[authToken] = info;

        if (clientIp != null)
        {
            var ipKey = NormalizeIp(clientIp);
            _ipToTokenMapping[ipKey] = authToken;
            if (clientIp.IsIPv4MappedToIPv6)
                _ipToTokenMapping[clientIp.MapToIPv4().ToString()] = authToken;
        }

        if (!string.IsNullOrEmpty(friendCode))
            _friendCodeToTokenMapping[friendCode] = authToken;

        _logger.LogDebug(
            "Auth cached: PUID={Puid}, FriendCode={FriendCode}, IP={Ip}",
            productUserId, friendCode ?? "(none)", clientIp?.ToString() ?? "(none)");

        ScheduleCleanup();
    }

    /// <summary>
    /// 将 Nonce 关联到已有的认证条目（DTLS auth handler 生成 nonce 后调用）。
    /// </summary>
    public static void BindNonce(string authToken, uint nonce)
    {
        _nonceToToken[nonce] = authToken;
        _logger.LogDebug("Nonce {Nonce} bound to auth token", nonce);
    }

    /// <summary>
    /// 通过 Nonce 查找认证信息（最可靠，不依赖 IP）。
    /// </summary>
    public static UserAuthInfo? GetUserAuthByNonce(uint nonce)
    {
        if (_nonceToToken.TryGetValue(nonce, out var token))
            return GetUserAuthByToken(token);
        return null;
    }

    public static UserAuthInfo? GetUserAuthByToken(string matchmakerToken)
    {
        if (_tokenCache.TryGetValue(matchmakerToken, out var info))
        {
            if (IsExpired(info)) { _tokenCache.TryRemove(matchmakerToken, out _); return null; }
            return info;
        }
        return null;
    }

    public static UserAuthInfo? GetUserAuthByIp(IPAddress clientIp)
    {
        if (clientIp == null) return null;
        var ipKey = NormalizeIp(clientIp);
        if (_ipToTokenMapping.TryGetValue(ipKey, out var token))
            return GetUserAuthByToken(token);
        if (clientIp.IsIPv4MappedToIPv6)
        {
            var v4Key = clientIp.MapToIPv4().ToString();
            if (_ipToTokenMapping.TryGetValue(v4Key, out token))
                return GetUserAuthByToken(token);
        }
        return null;
    }

    public static UserAuthInfo? GetUserAuthByFriendCode(string friendCode)
    {
        if (_friendCodeToTokenMapping.TryGetValue(friendCode, out var token))
            return GetUserAuthByToken(token);
        return null;
    }

    public static UserAuthInfo? GetUserAuthByPuid(string productUserId)
    {
        return _tokenCache.Values.FirstOrDefault(x => x.ProductUserId == productUserId && !IsExpired(x));
    }

    public static (int TokenCount, int IpMappingCount) GetCacheStats()
        => (_tokenCache.Count, _ipToTokenMapping.Count);

    private static string NormalizeIp(IPAddress ip)
        => ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4().ToString() : ip.ToString();

    private static bool IsExpired(UserAuthInfo info)
        => DateTime.UtcNow - info.Timestamp > AuthEntryTtl;

    private static async void ScheduleCleanup()
    {
        await Task.Delay(TimeSpan.FromMinutes(5));
        CleanupExpiredCache();
    }

    private static void CleanupExpiredCache()
    {
        var expired = _tokenCache.Where(kv => IsExpired(kv.Value)).Select(kv => kv.Key).ToList();
        foreach (var key in expired) _tokenCache.TryRemove(key, out _);

        var valid = new HashSet<string>(_tokenCache.Keys);
        foreach (var key in _ipToTokenMapping.Where(kv => !valid.Contains(kv.Value)).Select(kv => kv.Key).ToList())
            _ipToTokenMapping.TryRemove(key, out _);
        foreach (var key in _friendCodeToTokenMapping.Where(kv => !valid.Contains(kv.Value)).Select(kv => kv.Key).ToList())
            _friendCodeToTokenMapping.TryRemove(key, out _);
        foreach (var key in _nonceToToken.Where(kv => !valid.Contains(kv.Value)).Select(kv => kv.Key).ToList())
            _nonceToToken.TryRemove(key, out _);

        if (expired.Count > 0)
            _logger.LogDebug("Cleaned up {Count} expired auth entries", expired.Count);
    }
}

public class UserAuthInfo
{
    public string ProductUserId { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string? EosToken { get; set; }
    public string FriendCode { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? ClientIp { get; set; }
    public string? PlayerName { get; set; }
}
