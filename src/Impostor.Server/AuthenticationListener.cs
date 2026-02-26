using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Impostor.Api.Events.Managers;
using Impostor.Api.Net.Messages.C2S;
using Impostor.Hazel;
using Impostor.Hazel.Dtls;
using Impostor.Server.Net.Hazel;
using Impostor.Server.Service;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace Impostor.Server.Net
{
    /// <summary>
    /// 认证监听器 - 监听 DTLS 认证端口（gamePort + 2）
    /// 
    /// 客户端连接流程：
    /// 1. 客户端先连接到认证端口 (19881)
    /// 2. 发送 AuthHandshake (matchmakerToken + friendCode)
    /// 3. 服务器生成唯一的 Nonce
    /// 4. 服务器返回 Nonce 给客户端
    /// 5. 客户端在游戏握手时将 Nonce 作为 LastNonceReceived 发送
    /// 6. 游戏服务器通过 Nonce 查找认证信息（完全不依赖 IP）
    /// </summary>
    internal class AuthenticationListener
    {
        private readonly IEventManager _eventManager;
        private readonly ObjectPool<MessageReader> _readerPool;
        private readonly ILogger<AuthenticationListener> _logger;
        private DtlsConnectionListener? _connection;

        public AuthenticationListener(
            IEventManager eventManager,
            ObjectPool<MessageReader> readerPool,
            ILogger<AuthenticationListener> logger)
        {
            _eventManager = eventManager;
            _readerPool = readerPool;
            _logger = logger;
        }

        public async ValueTask StartAsync(IPEndPoint ipEndPoint)
        {
            var mode = ipEndPoint.AddressFamily switch
            {
                AddressFamily.InterNetwork => IPMode.IPv4,
                AddressFamily.InterNetworkV6 => IPMode.IPv6,
                _ => throw new InvalidOperationException(),
            };

            _connection = new DtlsConnectionListener(ipEndPoint, _readerPool, mode, false)
            {
                NewConnection = OnNewConnection,
            };

            await _connection.StartAsync();
            _logger.LogInformation("🔐 Authentication listener started on {EndPoint} (DTLS port)", ipEndPoint);
        }

        public async ValueTask StopAsync()
        {
            if (_connection != null)
            {
                await _connection.DisposeAsync();
            }
        }

        private async ValueTask OnNewConnection(NewConnectionEventArgs e)
        {
            try
            {
                // 解析认证握手
                AuthHandshakeC2S.Deserialize(
                    e.HandshakeData,
                    out var clientVersion,
                    out var matchmakerToken,
                    out var friendCode);

                _logger.LogDebug("🔐 Auth handshake received: Token={Token}, FriendCode={FriendCode}",
                    matchmakerToken.Length > 20 ? matchmakerToken[..20] + "..." : matchmakerToken,
                    friendCode);

                // 验证 matchmakerToken 是否在缓存中
                var authInfo = AuthCacheService.GetUserAuthByToken(matchmakerToken);
                if (authInfo == null)
                {
                    _logger.LogWarning("🔐 Auth failed: matchmakerToken not found in cache. Token={Token}",
                        matchmakerToken.Length > 20 ? matchmakerToken[..20] + "..." : matchmakerToken);
                    await e.Connection.DisconnectAsync(DisconnectReason.Custom);
                    return;
                }

                // ★ 生成 Nonce
                var nonce = GenerateNonce();

                // ★ 将 Nonce 绑定到认证信息
                AuthCacheService.BindNonce(matchmakerToken, nonce);

                _logger.LogInformation(
                    "✓ 🔐 Authentication successful: PUID={Puid}, FriendCode={FriendCode}, Nonce={Nonce}",
                    authInfo.ProductUserId, authInfo.FriendCode, nonce);

                // ★ 返回 Nonce 给客户端
                using var responseWriter = MessageWriter.Get(MessageType.Reliable);
                responseWriter.WriteByte(1); // Auth response tag
                responseWriter.WriteUInt32(nonce);

                await e.Connection.SendAsync(responseWriter);

                // 立即断开连接（只用于传递 Nonce）
                await Task.Delay(100); // 给客户端时间接收 nonce
                await e.Connection.DisconnectAsync(DisconnectReason.Custom);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔐 Error in authentication listener");
                try
                {
                    await e.Connection.DisconnectAsync(DisconnectReason.Custom);
                }
                catch { }
            }
        }

        private uint GenerateNonce()
        {
            // 生成随机 nonce（确保不为0）
            while (true)
            {
                var bytes = new byte[4];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(bytes);
                }
                var nonce = BitConverter.ToUInt32(bytes, 0);
                if (nonce != 0)
                {
                    return nonce;
                }
            }
        }
    }
}
