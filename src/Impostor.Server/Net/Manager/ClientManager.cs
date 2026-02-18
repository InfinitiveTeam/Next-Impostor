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
                // Super rare but reset the _idLast because of overflow.
                _idLast = 0;

                // And get a new id.
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

            var clientIp = connection.EndPoint.Address;
            string? productUserId = null;
            string? friendCode = null;

            // === 核心认证逻辑 ===
            // 优先方案 1：从握手包中的 matchmakerToken 解析（最可靠，不依赖 IP）
            if (!string.IsNullOrEmpty(matchmakerToken))
            {
                var authInfo = AuthCacheService.GetUserAuthByToken(matchmakerToken);
                if (authInfo != null)
                {
                    productUserId = authInfo.ProductUserId;
                    friendCode = authInfo.FriendCode;
                    _logger.LogInformation(
                        "Client {Name} authenticated via matchmakerToken: PUID={Puid}, FriendCode={FriendCode}, IP={Ip}",
                        name, productUserId, friendCode, clientIp);
                }
                else
                {
                    _logger.LogWarning(
                        "Client {Name} sent matchmakerToken but it was not found in cache. IP={Ip}, Token={Token}",
                        name, clientIp, matchmakerToken.Length > 20 ? matchmakerToken[..20] + "..." : matchmakerToken);
                }
            }

            // 优先方案 2：握手包中直接携带了 friendCode（DTLS 模式）
            if (productUserId == null && !string.IsNullOrEmpty(handshakeFriendCode))
            {
                // 通过 friendCode 在缓存中查找对应的 PUID
                var authInfo = AuthCacheService.GetUserAuthByFriendCode(handshakeFriendCode);
                if (authInfo != null)
                {
                    productUserId = authInfo.ProductUserId;
                    friendCode = handshakeFriendCode;
                    _logger.LogInformation(
                        "Client {Name} authenticated via handshake friendCode: PUID={Puid}, IP={Ip}",
                        name, productUserId, clientIp);
                }
            }

            // 回退方案：通过 IP 精确匹配（不允许使用"最近玩家"猜测）
            if (productUserId == null)
            {
                var authByIp = AuthCacheService.GetUserAuthByIp(clientIp);
                if (authByIp != null)
                {
                    productUserId = authByIp.ProductUserId;
                    friendCode = authByIp.FriendCode;
                    _logger.LogInformation(
                        "Client {Name} authenticated via IP match: PUID={Puid}, IP={Ip}",
                        name, productUserId, clientIp);
                }
                else
                {
                    _logger.LogWarning(
                        "Client {Name} connected without authentication. IP={Ip}",
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
                // 未认证玩家：使用稳定的基于 IP+Name 的回退标识（不随机，不猜测他人）
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
