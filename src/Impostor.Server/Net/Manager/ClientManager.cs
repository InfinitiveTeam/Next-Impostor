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
        private readonly SafePUIDMapper _puidMapper;
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
            _puidMapper = new SafePUIDMapper();

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

            // 获取 UDP 连接的客户端 IP，将 IPv4-mapped IPv6 规范化为纯 IPv4
            // 这样才能与 HTTP 认证时存储的 IPv4 地址正确匹配
            var rawClientIp = connection.EndPoint.Address;
            var clientIp = rawClientIp.IsIPv4MappedToIPv6 ? rawClientIp.MapToIPv4() : rawClientIp;
            string? productUserId = null;
            string? friendCode = null;

            // === 核心认证逻辑 ===
            // 优先方案 1：通过 Nonce 匹配（最可靠，不依赖 IP）
            // HandshakeC2S 将 LastNonceReceived 包装为 "NONCE:{uint}" 字符串
            if (!string.IsNullOrEmpty(matchmakerToken) && matchmakerToken.StartsWith("NONCE:", StringComparison.Ordinal))
            {
                if (uint.TryParse(matchmakerToken.AsSpan(6), out var nonce))
                {
                    var authInfo = AuthCacheService.GetUserAuthByNonce(nonce);
                    if (authInfo != null)
                    {
                        productUserId = authInfo.ProductUserId;
                        friendCode = authInfo.FriendCode;
                        _logger.LogInformation(
                            "Client {Name} authenticated via nonce: FriendCode={FriendCode}, IP={Ip}",
                            name, friendCode, clientIp);
                        matchmakerToken = null; // 已处理，清空避免下面重复查找
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Client {Name} nonce {Nonce} not found in cache, falling back. IP={Ip}",
                            name, nonce, clientIp);
                        matchmakerToken = null;
                    }
                }
            }

            // 优先方案 2：从握手包中的 matchmakerToken 解析
            if (productUserId == null && !string.IsNullOrEmpty(matchmakerToken))
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

            // 优先方案 3：握手包中直接携带了 friendCode
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
                else
                {
                    // 即使无法通过缓存反查认证信息，也保留握手中携带的 FriendCode。
                    // 这能避免错误地回退到 Name#XXXX，并使玩家显示正确的 FriendCode。
                    friendCode = handshakeFriendCode;
                    _logger.LogInformation(
                        "Client {Name} provided handshake friendCode without auth cache hit, use handshake FriendCode directly: {FriendCode}, IP={Ip}",
                        name, friendCode, clientIp);
                }
            }

            // 回退方案：通过 IP 精确匹配
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
            
            // === PUID 映射检查 ===
            // 防止同一 PUID 的多个连接（在 NAT 环境中很关键）
            if (!string.IsNullOrEmpty(productUserId) && client is Client clientWithAuth)
            {
                // 尝试注册 PUID
                if (!_puidMapper.TryRegisterPUID(id, productUserId))
                {
                    // PUID 已在线 - 这不应该发生，日志警告
                    _logger.LogWarning(
                        "PUID {Puid} is already online from another client. Disconnecting new connection. ClientName={Name}, NewClientId={ClientId}",
                        productUserId, name, id);
                    
                    // 踢出新连接
                    await connection.CustomDisconnectAsync(DisconnectReason.Custom, "Your account is already logged in elsewhere.");
                    return;
                }
            }
            
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
            
            // === PUID 映射清理 ===
            // 防止内存泄漏和PUID卡住
            _puidMapper.TryUnregisterPUID(client.Id);
            
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
