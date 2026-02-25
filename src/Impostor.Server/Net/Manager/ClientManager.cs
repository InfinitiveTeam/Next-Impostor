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

        // ★ 修改：添加 productUserId 参数
        public async ValueTask RegisterConnectionAsync(
            IHazelConnection connection, 
            string name, 
            GameVersion clientVersion, 
            Language language, 
            QuickChatModes chatMode, 
            PlatformSpecificData? platformSpecificData, 
            string? matchmakerToken = null, 
            string? handshakeFriendCode = null,
            string? productUserId = null)  // ★ 新增：从握手中获取的 ProductUserId
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

            // 获取 UDP 连接的客户端 IP
            var rawClientIp = connection.EndPoint.Address;
            var clientIp = rawClientIp.IsIPv4MappedToIPv6 ? rawClientIp.MapToIPv4() : rawClientIp;
            string? authProductUserId = null;
            string? friendCode = null;

            // ★ 修改：优先级顺序更改为优先使用握手中的 ProductUserId（最可靠）
            
            // 优先方案 1：握手中的 ProductUserId（最可靠）
            // AU 客户端会在握手时自动发送 ProductUserId（EOS 账号 ID）
            if (!string.IsNullOrEmpty(productUserId))
            {
                _logger.LogDebug(
                    "Client {Name} provided ProductUserId in handshake: {ProductUserId}",
                    name, productUserId);
                
                var authInfo = AuthCacheService.GetUserAuthByPuid(productUserId);
                if (authInfo != null)
                {
                    authProductUserId = authInfo.ProductUserId;
                    friendCode = authInfo.FriendCode;
                    _logger.LogInformation(
                        "Client {Name} authenticated via ProductUserId (PUID={PUID}): FriendCode={FriendCode}",
                        name, productUserId, friendCode);
                }
                else
                {
                    _logger.LogWarning(
                        "Client {Name} ProductUserId {ProductUserId} not found in auth cache, falling back",
                        name, productUserId);
                }
            }

            // 优先方案 2：从握手包中的 matchmakerToken
            if (authProductUserId == null && !string.IsNullOrEmpty(matchmakerToken))
            {
                var authInfo = AuthCacheService.GetUserAuthByToken(matchmakerToken);
                if (authInfo != null)
                {
                    authProductUserId = authInfo.ProductUserId;
                    friendCode = authInfo.FriendCode;
                    _logger.LogInformation(
                        "Client {Name} authenticated via matchmakerToken: PUID={Puid}, FriendCode={FriendCode}",
                        name, authProductUserId, friendCode);
                }
                else
                {
                    _logger.LogWarning(
                        "Client {Name} sent matchmakerToken but it was not found in cache. Token={Token}",
                        name, matchmakerToken.Length > 20 ? matchmakerToken[..20] + "..." : matchmakerToken);
                }
            }

            // 优先方案 3：握手包中直接携带的 friendCode
            if (authProductUserId == null && !string.IsNullOrEmpty(handshakeFriendCode))
            {
                var authInfo = AuthCacheService.GetUserAuthByFriendCode(handshakeFriendCode);
                if (authInfo != null)
                {
                    authProductUserId = authInfo.ProductUserId;
                    friendCode = handshakeFriendCode;
                    _logger.LogInformation(
                        "Client {Name} authenticated via handshake friendCode: PUID={Puid}",
                        name, authProductUserId);
                }
                else
                {
                    friendCode = handshakeFriendCode;
                    _logger.LogInformation(
                        "Client {Name} provided handshake friendCode without auth cache hit: {FriendCode}",
                        name, friendCode);
                }
            }

            // 回退方案 4：通过 IP 精确匹配（仅当没有其他认证方式时）
            if (authProductUserId == null)
            {
                _logger.LogDebug("Attempting IP-based fallback authentication for client {Name} from {ClientIp}", name, clientIp);
                
                var authByIp = AuthCacheService.GetUserAuthByIp(clientIp);
                if (authByIp != null)
                {
                    authProductUserId = authByIp.ProductUserId;
                    friendCode = authByIp.FriendCode;
                    _logger.LogInformation(
                        "Client {Name} authenticated via IP match (fallback): PUID={Puid}, IP={Ip}",
                        name, authProductUserId, clientIp);
                }
                else
                {
                    _logger.LogWarning(
                        "Client {Name} connected without any valid authentication method. IP={Ip}",
                        name, clientIp);
                }
            }

            var client = _clientFactory.Create(connection, name, clientVersion, language, chatMode, platformSpecificData);

            if (!string.IsNullOrEmpty(authProductUserId) && client is Client concreteClient)
            {
                concreteClient.ProductUserId = authProductUserId;
                concreteClient.FriendCode = friendCode ?? GenerateFallbackFriendCode(authProductUserId);
            }
            else if (client is Client concreteClient2)
            {
                // 未认证玩家
                var stableId = $"UNAUTH_{clientIp}_{name}";
                concreteClient2.ProductUserId = $"UNAUTH_{GenerateStableDiscriminator(stableId)}";
                concreteClient2.FriendCode = $"{name}#{GenerateStableDiscriminator(stableId)}";
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
