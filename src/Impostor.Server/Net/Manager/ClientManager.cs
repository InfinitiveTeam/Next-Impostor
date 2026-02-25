using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private int _idLast;

        public ClientManager(
            ILogger<ClientManager> logger,
            IEventManager eventManager,
            IClientFactory clientFactory,
            ICompatibilityManager compatibilityManager,
            IOptions<CompatibilityConfig> compatibilityConfig)
        {
            _logger = logger;
            _eventManager = eventManager;
            _clientFactory = clientFactory;
            _clients = new ConcurrentDictionary<int, ClientBase>();
            _compatibilityManager = compatibilityManager;
            _compatibilityConfig = compatibilityConfig.Value;

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
                _idLast = 0;
                clientId = Interlocked.Increment(ref _idLast);
            }

            return clientId;
        }

        public async ValueTask RegisterConnectionAsync(IHazelConnection connection, string name, GameVersion clientVersion, Language language, QuickChatModes chatMode, PlatformSpecificData? platformSpecificData, string? matchmakerToken = null, string? handshakeFriendCode = null)
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

            if (clientVersion.HasDisableServerAuthorityFlag)
            {
                _logger.LogInformation("Player {Name} connected with server authority disabled. Continue connection and FriendCode assignment.", name);
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

            var rawClientIp = connection.EndPoint.Address;
            var clientIp = rawClientIp.IsIPv4MappedToIPv6 ? rawClientIp.MapToIPv4() : rawClientIp;
            string? productUserId = null;
            string? friendCode = null;

            // ★★★ 关键修改：改进的认证优先级
            // 现在优先使用 matchmakerToken 通过 HTTP 缓存进行认证
            // 这确保了玩家身份是通过正式的 HTTP 认证验证的，而不仅仅依靠 IP
            
            // 优先方案 1：通过 matchmakerToken 查询 HTTP 缓存的认证信息（最可靠）
            // matchmakerToken 来自于客户端的 HTTP /api/user/token 请求
            // 这是最可信的认证方式，因为服务器已验证了客户端的 EOS Token
            if (!string.IsNullOrEmpty(matchmakerToken))
            {
                // 首先尝试作为 NONCE
                if (matchmakerToken.StartsWith("NONCE:", StringComparison.Ordinal))
                {
                    if (uint.TryParse(matchmakerToken.AsSpan(6), out var nonce))
                    {
                        var authInfo = AuthCacheService.GetUserAuthByNonce(nonce);
                        if (authInfo != null)
                        {
                            productUserId = authInfo.ProductUserId;
                            friendCode = authInfo.FriendCode;
                            _logger.LogInformation(
                                "✓ Client {Name} authenticated via NONCE: FriendCode={FriendCode}",
                                name, friendCode);
                            matchmakerToken = null;
                        }
                    }
                }
                
                // 如果不是 NONCE，或 NONCE 查询失败，尝试作为 matchmakerToken
                if (productUserId == null)
                {
                    var authInfo = AuthCacheService.GetUserAuthByToken(matchmakerToken);
                    if (authInfo != null)
                    {
                        productUserId = authInfo.ProductUserId;
                        friendCode = authInfo.FriendCode;
                        _logger.LogInformation(
                            "✓ Client {Name} authenticated via HTTP matchmakerToken: PUID={Puid}, FriendCode={FriendCode}",
                            name, productUserId, friendCode);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "✗ Client {Name} provided matchmakerToken but not found in cache. Token={Token}",
                            name, matchmakerToken.Length > 20 ? matchmakerToken[..20] + "..." : matchmakerToken);
                    }
                }
            }

            // 优先方案 2：握手中的 FriendCode（在非DTLS客户端或改进的客户端中）
            if (productUserId == null && !string.IsNullOrEmpty(handshakeFriendCode))
            {
                var authInfo = AuthCacheService.GetUserAuthByFriendCode(handshakeFriendCode);
                if (authInfo != null)
                {
                    productUserId = authInfo.ProductUserId;
                    friendCode = handshakeFriendCode;
                    _logger.LogInformation(
                        "✓ Client {Name} authenticated via handshake FriendCode: PUID={Puid}",
                        name, productUserId);
                }
                else
                {
                    // 即使缓存中未找到，仍然保留握手中的 FriendCode（可能来自已过期的认证）
                    friendCode = handshakeFriendCode;
                    _logger.LogInformation(
                        "⚠ Client {Name} provided handshake FriendCode without cache hit: {FriendCode}",
                        name, friendCode);
                }
            }

            // 回退方案：通过 IP 地址匹配（当没有其他认证方式时）
            if (productUserId == null)
            {
                var authByIp = AuthCacheService.GetUserAuthByIp(clientIp);
                if (authByIp != null)
                {
                    productUserId = authByIp.ProductUserId;
                    friendCode = authByIp.FriendCode;
                    _logger.LogWarning(
                        "⚠ Client {Name} authenticated via IP match (fallback): PUID={Puid}, IP={Ip}",
                        name, productUserId, clientIp);
                }
                else
                {
                    _logger.LogError(
                        "✗ Client {Name} connected without any valid authentication. IP={Ip}",
                        name, clientIp);
                }
            }

            var client = _clientFactory.Create(connection, name, clientVersion, language, chatMode, platformSpecificData);

            if (!string.IsNullOrEmpty(productUserId) && client is Client concreteClient)
            {
                concreteClient.ProductUserId = productUserId;
                concreteClient.FriendCode = friendCode ?? GenerateFallbackFriendCode(productUserId);
            }
            else if (client is Client concreteClient2)
            {
                var stableId = $"UNAUTH_{clientIp}_{name}";
                concreteClient2.ProductUserId = $"UNAUTH_{GenerateStableDiscriminator(stableId)}";
                concreteClient2.FriendCode = $"{System.Text.RegularExpressions.Regex.Replace(name, "[^a-zA-Z0-9]", "Player")}#{GenerateStableDiscriminator(stableId)}";
            }

            var id = NextId();
            client.Id = id;
            _logger.LogTrace("Client connected with ID: {ClientId}, IP: {ClientIp}, PUID: {Puid}, FriendCode: {FriendCode}",
                id, clientIp, client.ProductUserId, client.FriendCode);
            _clients.TryAdd(id, client);

            await _eventManager.CallAsync(new ClientConnectedEvent(connection, client));
        }

        private string GenerateFallbackFriendCode(string productUserId)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(productUserId));
            var discriminator = BitConverter.ToUInt16(hash, 0) % 10000;
            return $"Player#{discriminator:D4}";
        }

        private string GenerateStableDiscriminator(string input)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            var discriminator = BitConverter.ToUInt16(hash, 0) % 10000;
            return discriminator.ToString("D4");
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
