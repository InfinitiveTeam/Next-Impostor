using System;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Impostor.Api.Innersloth;
using Impostor.Server.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Http;

[Route("/api/user")]
[ApiController]
public sealed class TokenController : ControllerBase
{
    private readonly ILogger<TokenController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    // 缓存 FriendCode，减少对后端的不必要请求
    private static readonly ConcurrentDictionary<string, CachedFriendCode> FriendCodeCache = new();
    private static readonly TimeSpan FriendCodeCacheDuration = TimeSpan.FromMinutes(10);

    private class CachedFriendCode
    {
        public string? FriendCode { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public TokenController(ILogger<TokenController> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Get an authentication token.
    /// The client sends EOS Bearer token in Authorization header.
    /// We validate it, fetch the FriendCode from Innersloth backend,
    /// then return a signed matchmakerToken (base64) which the client
    /// will embed in the UDP handshake. This is the only reliable way
    /// to associate a UDP connection with an authenticated identity.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> GetToken([FromBody] TokenRequest request, [FromHeader] string authorization)
    {
        try
        {
            // 验证 Authorization 头
            if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
            {
                _logger.LogWarning("Missing or invalid Authorization header");
                return Unauthorized(new { error = "Missing or invalid authorization" });
            }

            var eosToken = authorization.Substring("Bearer ".Length);

            // 解析 EOS UserIDToken (JWT)
            var tokenHandler = new JwtSecurityTokenHandler();
            if (!tokenHandler.CanReadToken(eosToken))
            {
                _logger.LogWarning("Invalid JWT token format");
                return Unauthorized(new { error = "Invalid token format" });
            }

            var jwtToken = tokenHandler.ReadJwtToken(eosToken);

            // 从 JWT 中提取 PUID
            var productUserId = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == "puid")?.Value;

            if (string.IsNullOrEmpty(productUserId))
            {
                _logger.LogWarning("Could not extract ProductUserId from token");
                return Unauthorized(new { error = "Invalid token content" });
            }

            // 获取客户端真实 IP 地址：
            // 优先使用 X-Real-IP / X-Forwarded-For 头（适用于 Nginx/HAProxy 反向代理场景）
            // 这样 HTTP 认证时记录的 IP 与后续 UDP 连接的 IP 能正确匹配。
            System.Net.IPAddress? clientIp = null;
            var xRealIp = HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            var xForwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xRealIp) && System.Net.IPAddress.TryParse(xRealIp, out var realIp))
            {
                clientIp = realIp;
            }
            else if (!string.IsNullOrEmpty(xForwardedFor))
            {
                // X-Forwarded-For 可能包含多个 IP，取第一个（最原始客户端 IP）
                var firstIp = xForwardedFor.Split(",")[0].Trim();
                if (System.Net.IPAddress.TryParse(firstIp, out var forwardedIp))
                {
                    clientIp = forwardedIp;
                }
            }
            // 回退到直连 IP
            clientIp ??= HttpContext.Connection.RemoteIpAddress;
            // 如果是 IPv4-mapped IPv6 地址（::ffff:1.2.3.4），映射为纯 IPv4
            if (clientIp != null && clientIp.IsIPv4MappedToIPv6)
            {
                clientIp = clientIp.MapToIPv4();
            }

            // 从 Innersloth 后端获取 FriendCode（使用缓存）
            var friendCode = await GetFriendCodeFromBackendAsync(eosToken, productUserId);

            if (string.IsNullOrEmpty(friendCode))
            {
                // 生成稳定的回退 FriendCode（基于 PUID 哈希，确保同一玩家每次相同）
                friendCode = GenerateFallbackFriendCode(productUserId);
                _logger.LogWarning(
                    "Could not fetch FriendCode from backend for PUID={Puid}, using fallback: {FriendCode}",
                    productUserId, friendCode);
            }

            // 生成 matchmakerToken（这是客户端握手时会发送回来的 token）
            var matchmakerToken = GenerateMatchmakerToken(productUserId, request.ClientVersion);

            // 存储认证信息到缓存，key 为 matchmakerToken（最重要的索引）
            AuthCacheService.AddUserAuth(
                productUserId: productUserId,
                authToken: matchmakerToken,   // 存 matchmakerToken 作为查找 key
                eosToken: eosToken,
                friendCode: friendCode,
                clientIp: clientIp,
                playerName: request.Username,
                clientVersion: request.ClientVersion);

            _logger.LogInformation(
                "User authenticated: PUID={Puid}, FriendCode={FriendCode}, IP={Ip}",
                productUserId, friendCode, clientIp);

            if (request.Nonce != 0)
            {
                AuthCacheService.BindNonce(matchmakerToken, request.Nonce);
                _logger.LogInformation(
                    "Bound Nonce {Nonce} to auth for PUID={Puid}",
                    request.Nonce, productUserId);
            }

            var token = new Token
            {
                Content = new TokenPayload
                {
                    ProductUserId = productUserId,
                    FriendCode = friendCode,
                    ClientVersion = request.ClientVersion
                },
                Hash = matchmakerToken  // Hash 字段复用为 matchmakerToken，客户端会在握手时原样发送
            };

            var serialized = JsonSerializer.SerializeToUtf8Bytes(token);
            return Ok(Convert.ToBase64String(serialized));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing token request");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private async Task<string?> GetFriendCodeFromBackendAsync(string eosToken, string productUserId)
    {
        // 1. 尝试从缓存获取
        if (FriendCodeCache.TryGetValue(productUserId, out var cached))
        {
            if (cached.ExpiresAt > DateTime.UtcNow)
            {
                return cached.FriendCode; // 可能为 null（之前获取失败也缓存）
            }
            // 过期则移除
            FriendCodeCache.TryRemove(productUserId, out _);
        }

        // 2. 缓存未命中或过期，请求后端
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            var request = new HttpRequestMessage(HttpMethod.Get, "https://backend.innersloth.com/api/user/username");
            request.Headers.Add("Authorization", "Bearer " + eosToken);
            request.Headers.Add("User-Agent", "UnityPlayer/2022.3.44f1 (UnityWebRequest/1.0, libcurl/7.84.0-DEV)");
            request.Headers.Add("X-Unity-Version", "2022.3.44f1");
            // Content-Type 头告知 Innersloth 后端返回 JSON:API 格式（与 Among Us 客户端行为一致）
            request.Headers.TryAddWithoutValidation("Accept", "application/vnd.api+json");

            var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);

                // Innersloth 后端返回 JSON:API 格式：
                //   { "data": { "attributes": { "username": "...", "discriminator": "..." } } }
                // 也兼容扁平格式：
                //   { "username": "...", "discriminator": "..." }
                JsonElement attrElem;
                if (doc.RootElement.TryGetProperty("data", out var dataElem))
                {
                    // JSON:API 格式 - 取 attributes 子对象
                    if (!dataElem.TryGetProperty("attributes", out attrElem))
                    {
                        attrElem = dataElem;
                    }
                }
                else
                {
                    // 扁平格式
                    attrElem = doc.RootElement;
                }

                var username = attrElem.TryGetProperty("username", out var u) ? u.GetString() : null;
                var discriminator = attrElem.TryGetProperty("discriminator", out var d) ? d.GetString() : null;

                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(discriminator))
                {
                    var friendCode = $"{username}#{discriminator}";
                    // 存入缓存
                    FriendCodeCache[productUserId] = new CachedFriendCode
                    {
                        FriendCode = friendCode,
                        ExpiresAt = DateTime.UtcNow.Add(FriendCodeCacheDuration)
                    };
                    _logger.LogInformation("FriendCode fetched from backend: PUID={Puid}, FriendCode={FriendCode}", productUserId, friendCode);
                    return friendCode;
                }

                _logger.LogWarning("Backend response missing username/discriminator for PUID={Puid}. Response: {Content}", productUserId, content);
            }
            else
            {
                _logger.LogWarning("Backend returned {Status} when fetching FriendCode for PUID {Puid}", response.StatusCode, productUserId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while fetching FriendCode from backend for PUID {Puid}", productUserId);
        }

        // 3. 失败时缓存一个 null 值，避免频繁重试
        FriendCodeCache[productUserId] = new CachedFriendCode
        {
            FriendCode = null,
            ExpiresAt = DateTime.UtcNow.Add(FriendCodeCacheDuration)
        };
        return null;
    }

    private string GenerateMatchmakerToken(string productUserId, int clientVersion)
    {
        // 生成一个基于 PUID + 时间 + 随机数的不可预测 token
        // 客户端会在 UDP 握手中原样发回，服务端通过此 token 找到对应的认证信息
        var random = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        var input = $"{productUserId}:{clientVersion}:{DateTime.UtcNow:yyyyMMddHHmm}:{Convert.ToBase64String(random)}";
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hash);
    }

    private string GenerateFallbackFriendCode(string productUserId)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(productUserId));
        var discriminator = BitConverter.ToUInt16(hash, 0) % 10000;
        return $"Player#{discriminator:D4}";
    }

    /// <summary>Body of the token request endpoint.</summary>
    public class TokenRequest
    {
        [JsonPropertyName("Puid")]
        public required string ProductUserId { get; init; }

        [JsonPropertyName("Username")]
        public required string Username { get; init; }

        [JsonPropertyName("ClientVersion")]
        public required int ClientVersion { get; init; }

        [JsonPropertyName("Language")]
        public required Language Language { get; init; }
    }

    /// <summary>Token that is returned to the user.</summary>
    public sealed class Token
    {
        [JsonPropertyName("Content")]
        public required TokenPayload Content { get; init; }

        /// <summary>复用为 matchmakerToken，客户端握手时会原样发回。</summary>
        [JsonPropertyName("Hash")]
        public required string Hash { get; init; }
    }

    /// <summary>Actual token contents.</summary>
    public sealed class TokenPayload
    {
        private static readonly DateTime DefaultExpiryDate = new(2012, 12, 21);

        [JsonPropertyName("Puid")]
        public required string ProductUserId { get; init; }

        [JsonPropertyName("FriendCode")]
        public string? FriendCode { get; init; }

        [JsonPropertyName("ClientVersion")]
        public required int ClientVersion { get; init; }

        [JsonPropertyName("ExpiresAt")]
        public DateTime ExpiresAt { get; init; } = DefaultExpiryDate;
        
        [JsonPropertyName("Nonce")]
        public uint Nonce { get; init; }
    }
}
