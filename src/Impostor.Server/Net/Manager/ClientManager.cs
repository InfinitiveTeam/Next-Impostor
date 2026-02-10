using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Impostor.Api.Config;
using Impostor.Api.Events.Managers;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Impostor.Api.Net.Manager;
using Impostor.Hazel;
using Impostor.Server.Events.Client;
using Impostor.Server.Net.Factories;
using Impostor.Server.Service;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impostor.Server.Net.Manager
{
    internal partial class ClientManager
    {
        private readonly ILogger<ClientManager> _logger;
        private readonly IEventManager _eventManager;
        private readonly ConcurrentDictionary<int, ClientBase> _clients;
        private readonly ICompatibilityManager _compatibilityManager;
        private readonly CompatibilityConfig _compatibilityConfig;
        private readonly IClientFactory _clientFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private int _idLast;

        public ClientManager(
            ILogger<ClientManager> logger,
            IEventManager eventManager,
            IClientFactory clientFactory,
            ICompatibilityManager compatibilityManager,
            IOptions<CompatibilityConfig> compatibilityConfig,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _eventManager = eventManager;
            _clientFactory = clientFactory;
            _clients = new ConcurrentDictionary<int, ClientBase>();
            _compatibilityManager = compatibilityManager;
            _compatibilityConfig = compatibilityConfig.Value;
            _httpClientFactory = httpClientFactory;

            if (_compatibilityConfig.AllowFutureGameVersions
                || _compatibilityConfig.AllowHostAuthority
                || _compatibilityConfig.AllowVersionMixing)
            {
                _logger.LogWarning("One or more compatibility options were enabled, please mention these when seeking support:");

                if (_compatibilityConfig.AllowFutureGameVersions)
                {
                    _logger.LogWarning("AllowFutureGameVersions, which allows future Among Us versions to connect that were unknown at the time this Impostor was built");
                }

                if (_compatibilityConfig.AllowHostAuthority)
                {
                    _logger.LogWarning("AllowHostAuthority, which allows game hosts to control more game features, but it uses less well tested code on the client, which causes some bugs");
                }

                if (_compatibilityConfig.AllowVersionMixing)
                {
                    _logger.LogWarning("AllowVersionMixing, which allows players to join games created on different game versions that they may not be 100% compatible with");
                }
            }
        }

        public IEnumerable<ClientBase> Clients => _clients.Values;

        public int NextId()
        {
            var clientId = Interlocked.Increment(ref _idLast);

            if (clientId < 1)
            {
                // Super rare but reset the _idLast because of overflow.
                _idLast = 0;

                // And get a new id.
                clientId = Interlocked.Increment(ref _idLast);
            }

            return clientId;
        }

        public async ValueTask RegisterConnectionAsync(IHazelConnection connection, string name, GameVersion clientVersion, Language language, QuickChatModes chatMode, PlatformSpecificData? platformSpecificData)
        {
            var versionCompare = _compatibilityManager.CanConnectToServer(clientVersion);
            if (versionCompare == ICompatibilityManager.VersionCompareResult.ServerTooOld && _compatibilityConfig.AllowFutureGameVersions && platformSpecificData != null)
            {
                _logger.LogWarning("Client connected using future version: {clientVersion} ({version}). Unsupported, continue at your own risk.", clientVersion.Value, clientVersion.ToString());
            }
            else if (versionCompare != ICompatibilityManager.VersionCompareResult.Compatible || platformSpecificData == null)
            {
                _logger.LogInformation("Client connected using unsupported version: {clientVersion} ({version})", clientVersion.Value, clientVersion.ToString());

                using var packet = MessageWriter.Get(MessageType.Reliable);

                var message = versionCompare switch
                {
                    ICompatibilityManager.VersionCompareResult.ClientTooOld => DisconnectMessages.VersionClientTooOld,
                    ICompatibilityManager.VersionCompareResult.ServerTooOld => DisconnectMessages.VersionServerTooOld,
                    ICompatibilityManager.VersionCompareResult.Unknown => DisconnectMessages.VersionUnsupported,
                    _ => throw new ArgumentOutOfRangeException(),
                };

                await connection.CustomDisconnectAsync(DisconnectReason.Custom, message);
                return;
            }

            // Warn when players connect using the +25 flag that disables server authority.
            // This changes game behaviour, so we'd like to know if it's in use.
            if (clientVersion.HasDisableServerAuthorityFlag)
            {
                if (!_compatibilityConfig.AllowHostAuthority)
                {
                    _logger.LogInformation("Player {Name} kicked because they requested host authority.", name);
                    await connection.CustomDisconnectAsync(DisconnectReason.Custom, DisconnectMessages.HostAuthorityUnsupported);
                    return;
                }

                _logger.LogInformation("Player {Name} connected with server authority disabled, please mention that this mode is in use when asking for support.", name);
            }

            if (name.Length > 10)
            {
                await connection.CustomDisconnectAsync(DisconnectReason.Custom, DisconnectMessages.UsernameLength);
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                await connection.CustomDisconnectAsync(DisconnectReason.Custom, DisconnectMessages.UsernameIllegalCharacters);
                return;
            }

            string? productUserId = null;
            string? friendCode = null;
            UserAuthInfo? userInfo = null;

            // 1. 首先尝试通过IP地址获取认证信息
            var clientIp = connection.EndPoint.Address;
            userInfo = AuthCacheService.GetUserAuthByIp(clientIp);

            if (userInfo == null)
            {
                // 2. 如果IP找不到，尝试推断PUID
                productUserId = AuthCacheService.InferPuidFromConnection(clientIp, name, clientVersion);
                if (productUserId != null)
                {
                    userInfo = AuthCacheService.GetUserAuthByPuid(productUserId);
                }
            }
            else
            {
                productUserId = userInfo.ProductUserId;
            }

            // 3. 如果有认证信息，获取FriendCode
            if (userInfo != null && !string.IsNullOrEmpty(userInfo.AuthToken))
            {
                friendCode = await GetFriendCodeFromBackend(userInfo.AuthToken);

                if (string.IsNullOrEmpty(friendCode))
                {
                    friendCode = GenerateFallbackFriendCode(productUserId ?? "unknown");
                }
            }
            else
            {
                _logger.LogWarning("Client {Name} connected without authentication, IP: {ClientIp}",
                    name, clientIp);
            }

            var client = _clientFactory.Create(connection, name, clientVersion, language, chatMode, platformSpecificData);

            // 设置用户认证信息到Client对象
            if (!string.IsNullOrEmpty(productUserId) && !string.IsNullOrEmpty(friendCode) && client is Client concreteClient)
            {
                concreteClient.ProductUserId = productUserId;
                concreteClient.FriendCode = friendCode;

                _logger.LogInformation(
                    "Client {Name} authenticated with PUID: {Puid}, FriendCode: {FriendCode}, IP: {Ip}",
                    name, productUserId, friendCode, clientIp);
            }
            else
            {
                _logger.LogWarning("Client {Name} connected without proper authentication, IP: {ClientIp}",
                    name, clientIp);

                // 设置默认值或特殊标识
                if (client is Client concreteClient2)
                {
                    concreteClient2.ProductUserId = $"UNKNOWN_{Guid.NewGuid():N}";
                    concreteClient2.FriendCode = $"Guest#{new Random().Next(1000, 9999)}";
                }
            }

            var id = NextId();
            client.Id = id;
            _logger.LogTrace("Client connected with ID: {ClientId}, IP: {ClientIp}", id, clientIp);
            _clients.TryAdd(id, client);

            await _eventManager.CallAsync(new ClientConnectedEvent(connection, client));
        }
        private async Task<(bool success, (UserAuthInfo? userInfo, string? productUserId, string? friendCode) authInfo)>
    TryGetAuthInfoWithRetry(System.Net.IPAddress clientIp, string name, GameVersion clientVersion)
        {
            const int maxRetries = 2;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // 1. 首先尝试通过IP地址获取认证信息
                    var userInfo = AuthCacheService.GetUserAuthByIp(clientIp);
                    string? productUserId = null;

                    if (userInfo == null)
                    {
                        // 2. 如果IP找不到，尝试推断PUID
                        productUserId = AuthCacheService.InferPuidFromConnection(clientIp, name, clientVersion);
                        if (productUserId != null)
                        {
                            userInfo = AuthCacheService.GetUserAuthByPuid(productUserId);
                        }
                    }
                    else
                    {
                        productUserId = userInfo.ProductUserId;
                    }

                    // 3. 如果有认证信息，获取FriendCode
                    if (userInfo != null && !string.IsNullOrEmpty(userInfo.AuthToken))
                    {
                        var friendCode = await GetFriendCodeFromBackendWithRetry(userInfo.AuthToken, attempt);

                        if (!string.IsNullOrEmpty(friendCode))
                        {
                            return (true, (userInfo, productUserId, friendCode));
                        }

                        _logger.LogWarning("Attempt {Attempt} failed to get FriendCode for PUID: {Puid}",
                            attempt + 1, productUserId);
                    }
                    else
                    {
                        return (false, (null, null, null));
                    }

                    if (attempt < maxRetries)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1 * (attempt + 1)));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting auth info on attempt {Attempt}", attempt + 1);
                    if (attempt == maxRetries) break;
                    await Task.Delay(TimeSpan.FromSeconds(1 * (attempt + 1)));
                }
            }

            return (false, (null, null, null));
        }

        private async Task<string?> GetFriendCodeFromBackendWithRetry(string eosToken, int attempt)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                // 设置合理的超时时间
                client.Timeout = TimeSpan.FromSeconds(10 + attempt * 5);

                var request = new HttpRequestMessage(HttpMethod.Get, "https://backend.innersloth.com/api/user/username");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", eosToken);
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.api+json"));

                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return ParseFriendCodeFromResponse(content);
                }

                _logger.LogWarning("Backend API returned status {StatusCode} on attempt {Attempt}",
                    response.StatusCode, attempt + 1);
            }
            catch (TaskCanceledException) when (attempt < 2)
            {
                _logger.LogWarning("Backend API timeout on attempt {Attempt}", attempt + 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling backend API on attempt {Attempt}", attempt + 1);
            }

            return null;
        }

        private string? ParseFriendCodeFromResponse(string content)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(content);
                var dataElement = doc.RootElement.GetProperty("data");
                var attributesElement = dataElement.GetProperty("attributes");

                var username = attributesElement.GetProperty("username").GetString();
                var discriminator = attributesElement.GetProperty("discriminator").GetString();

                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(discriminator))
                {
                    return $"{username}#{discriminator}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse FriendCode response");
            }

            return null;
        }

        private async Task<(string productUserId, string friendCode)> GenerateStableFallbackInfo(
            IHazelConnection connection, string name, string? existingPuid)
        {
            // 基于连接信息生成稳定的回退ID，避免每次都是Guest
            var stableId = existingPuid ?? $"IP_{connection.EndPoint.Address}_{name}";

            // 使用更友好的命名
            var cleanName = System.Text.RegularExpressions.Regex.Replace(name, "[^a-zA-Z0-9]", "");
            if (string.IsNullOrEmpty(cleanName)) cleanName = "Player";

            var friendCode = $"{cleanName}#{GenerateStableDiscriminator(stableId)}";

            return (stableId, friendCode);
        }

        private string GenerateStableDiscriminator(string input)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            var discriminator = BitConverter.ToUInt16(hash, 0) % 10000;
            return discriminator.ToString("D4");
        }
        private async Task<string?> GetFriendCodeFromBackend(string eosToken)
        {
            var client = _httpClientFactory.CreateClient();

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://backend.innersloth.com/api/user/username");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", eosToken);
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.api+json"));

                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();

                    try
                    {
                        using JsonDocument doc = JsonDocument.Parse(content);
                        var dataElement = doc.RootElement.GetProperty("data");
                        var attributesElement = dataElement.GetProperty("attributes");

                        var username = attributesElement.GetProperty("username").GetString();
                        var discriminator = attributesElement.GetProperty("discriminator").GetString();

                        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(discriminator))
                        {
                            return $"{username}#{discriminator}";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to parse FriendCode response");
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to get FriendCode from backend. Status: {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling backend for FriendCode");
            }

            return null;
        }

        private string GenerateFallbackFriendCode(string productUserId)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(productUserId));
            var discriminator = BitConverter.ToUInt16(hash, 0) % 10000;
            return $"Player#{discriminator:D4}";
        }

        public void Remove(IClient client)
        {
            _logger.LogTrace("Client {ClientId} disconnected.", client.Id);
            _clients.TryRemove(client.Id, out _);
        }

        public bool Validate(IClient client)
        {
            return client.Id != 0
                   && _clients.TryGetValue(client.Id, out var registeredClient)
                   && ReferenceEquals(client, registeredClient);
        }
    }
}
