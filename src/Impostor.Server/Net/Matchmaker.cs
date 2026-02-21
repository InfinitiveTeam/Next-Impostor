using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Impostor.Api.Events.Managers;
using Impostor.Api.Net.Messages.C2S;
using Impostor.Hazel;
using Impostor.Hazel.Dtls;
using Impostor.Hazel.Udp;
using Impostor.Server.Events.Client;
using Impostor.Server.Net.Hazel;
using Impostor.Server.Net.Manager;
using Impostor.Server.Service;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace Impostor.Server.Net
{
    internal class Matchmaker
    {
        private readonly IEventManager _eventManager;
        private readonly ClientManager _clientManager;
        private readonly ObjectPool<MessageReader> _readerPool;
        private readonly ILogger<HazelConnection> _connectionLogger;
        private readonly ILogger<Matchmaker> _logger;
        private readonly DtlsCertificateService _certService;

        private UdpConnectionListener? _connection;
        private DtlsConnectionListener? _dtlsAuthListener;

        public Matchmaker(
            IEventManager eventManager,
            ClientManager clientManager,
            ObjectPool<MessageReader> readerPool,
            ILogger<HazelConnection> connectionLogger,
            ILogger<Matchmaker> logger,
            DtlsCertificateService certService)
        {
            _eventManager = eventManager;
            _clientManager = clientManager;
            _readerPool = readerPool;
            _connectionLogger = connectionLogger;
            _logger = logger;
            _certService = certService;
        }

        public async ValueTask StartAsync(IPEndPoint ipEndPoint)
        {
            var mode = ipEndPoint.AddressFamily switch
            {
                AddressFamily.InterNetwork => IPMode.IPv4,
                AddressFamily.InterNetworkV6 => IPMode.IPv6,
                _ => throw new InvalidOperationException(),
            };

            // 主 UDP 游戏监听器
            _connection = new UdpConnectionListener(ipEndPoint, _readerPool, mode)
            {
                NewConnection = OnNewConnection,
            };

            await _connection.StartAsync();
            _logger.LogInformation("UDP game listener started on {EndPoint}", ipEndPoint);

            // 尝试启动 DTLS 认证监听器（端口 +2）
            // Among Us 客户端在连接 UDP 游戏端口之前，会先通过此端口发送 EOS token 和 FriendCode
            var authEndPoint = new IPEndPoint(ipEndPoint.Address, ipEndPoint.Port + 2);
            try
            {
                var cert = _certService.GetOrCreateCertificate();
                _dtlsAuthListener = new DtlsConnectionListener(authEndPoint, _readerPool, mode)
                {
                    NewConnection = OnDtlsAuthConnection,
                };
                _dtlsAuthListener.SetCertificate(cert);
                await _dtlsAuthListener.StartAsync();
                _logger.LogInformation("DTLS auth listener started on {EndPoint} (game port+2)", authEndPoint);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to start DTLS auth listener on {EndPoint}. FriendCode falls back to IP/HTTP auth.", authEndPoint);
                _dtlsAuthListener = null;
            }
        }

        public async ValueTask StopAsync()
        {
            if (_connection != null)
            {
                await _connection.DisposeAsync();
            }

            if (_dtlsAuthListener != null)
            {
                await _dtlsAuthListener.DisposeAsync();
            }
        }

        /// <summary>
        /// 处理 DTLS 认证连接。
        /// Among Us 客户端（标准版或自定义服务器版）在连接游戏端口之前
        /// 会先连接此端口，发送 EOS token 和 FriendCode。
        /// 格式（AuthHandshakeC2S）：GameVersion(4B), Platform(1B), MatchmakerToken(string), FriendCode(string)
        /// </summary>
        private async ValueTask OnDtlsAuthConnection(NewConnectionEventArgs e)
        {
            var clientIp = e.Connection.EndPoint.Address;
            if (clientIp.IsIPv4MappedToIPv6)
            {
                clientIp = clientIp.MapToIPv4();
            }

            try
            {
                AuthHandshakeC2S.Deserialize(
                    e.HandshakeData,
                    out _,
                    out var matchmakerToken,
                    out var friendCode);

                if (!string.IsNullOrEmpty(friendCode))
                {
                    AuthCacheService.AddUserAuth(
                        productUserId: matchmakerToken ?? $"EOS_{clientIp}",
                        authToken: matchmakerToken ?? $"DTLS_{clientIp}",
                        friendCode: friendCode,
                        clientIp: clientIp);

                    _logger.LogInformation(
                        "DTLS auth from {Ip}: FriendCode={FriendCode}",
                        clientIp, friendCode);
                }
                else
                {
                    _logger.LogWarning("DTLS auth from {Ip}: empty FriendCode", clientIp);
                }

                // 发送 lastId 回客户端（客户端会在 UDP 握手中携带）
                var lastId = (uint)(DateTime.UtcNow.Ticks & 0xFFFF_FFFF);
                using var writer = MessageWriter.Get(MessageType.Reliable);
                writer.StartMessage(1);
                writer.Write(lastId);
                writer.EndMessage();
                await e.Connection.SendAsync(writer);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in DTLS auth from {Ip}", clientIp);
            }
        }

        private async ValueTask OnNewConnection(NewConnectionEventArgs e)
        {
            // Handshake.
            HandshakeC2S.Deserialize(
                e.HandshakeData,
                out var clientVersion,
                out var name,
                out var language,
                out var chatMode,
                out var platformSpecificData,
                out var matchmakerToken,
                out var friendCode);

            var connection = new HazelConnection(e.Connection, _connectionLogger);

            await _eventManager.CallAsync(new ClientConnectionEvent(connection, e.HandshakeData));

            await _clientManager.RegisterConnectionAsync(connection, name, clientVersion, language, chatMode, platformSpecificData, matchmakerToken, friendCode);
        }
    }
}
