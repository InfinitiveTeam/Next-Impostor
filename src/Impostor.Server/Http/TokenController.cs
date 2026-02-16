using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
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

    public TokenController(ILogger<TokenController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get an authentication token.
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

            // 从 JWT 中提取用户信息
            var productUserId = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == "puid")?.Value;

            if (string.IsNullOrEmpty(productUserId))
            {
                _logger.LogWarning("Could not extract ProductUserId from token");
                return Unauthorized(new { error = "Invalid token content" });
            }

            // 获取客户端IP地址
            var clientIp = HttpContext.Connection.RemoteIpAddress;

            // 存储用户认证信息到缓存，包含IP地址
            AuthCacheService.AddUserAuth(productUserId, eosToken, clientIp, request.Username, request.ClientVersion);

            _logger.LogInformation(
                "SUCCESS: User authenticated and cached: PUID={ProductUserId}, IP={ClientIp}",
                productUserId, clientIp);

            var token = new Token
            {
                Content = new TokenPayload
                {
                    ProductUserId = productUserId,
                    FriendCode = null, // 不再在token中返回FriendCode
                    ClientVersion = request.ClientVersion
                },
                Hash = GenerateTokenHash(productUserId, request.ClientVersion.ToString())
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

    private string GenerateTokenHash(string productUserId, string clientVersion)
    {
        var input = $"{productUserId}:{clientVersion}:{DateTime.UtcNow:yyyyMMdd}";
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hash).Replace("=", "").Substring(0, 16);
    }

    /// <summary>
    /// Body of the token request endpoint.
    /// </summary>
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

    /// <summary>
    /// Token that is returned to the user with a "signature".
    /// </summary>
    public sealed class Token
    {
        [JsonPropertyName("Content")]
        public required TokenPayload Content { get; init; }

        [JsonPropertyName("Hash")]
        public required string Hash { get; init; }
    }

    /// <summary>
    /// Actual token contents.
    /// </summary>
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
    }
}
