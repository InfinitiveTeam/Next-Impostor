using System;
using Impostor.Api.Innersloth;

namespace Impostor.Api.Net.Messages.C2S
{
    public static class HandshakeC2S
    {
        /// <summary>
        /// 解析标准 UDP 握手包（非 DTLS 布局）。
        ///
        /// 格式（Among Us 客户端 GetConnectionData，useDtls=false）：
        ///   GameVersion (4B)
        ///   Name (string)
        ///   [V1+] LastNonceReceived (uint32) — DTLS 已禁用，此字段总是 0
        ///   [V2+] Language (uint32) + ChatMode (byte)
        ///   [V3+] PlatformSpecificData (message) + ProductUserId (string) + CrossplayFlags (uint32)
        ///
        /// 认证流程（基于 ProductUserId，不依赖 IP）：
        ///   1. 客户端通过 HTTP 认证，获取 FriendCode
        ///   2. 游戏在握手时自动发送客户端的 ProductUserId
        ///   3. 服务器通过 ProductUserId 查询缓存的 FriendCode
        ///   4. 不再依赖 IP 匹配
        /// </summary>
        public static void Deserialize(
            IMessageReader reader,
            out GameVersion clientVersion,
            out string name,
            out Language language,
            out QuickChatModes chatMode,
            out PlatformSpecificData? platformSpecificData,
            out string? matchmakerToken,
            out string? friendCode,
            out string? productUserId)  // ★ 新增：返回握手中的 ProductUserId
        {
            clientVersion = reader.ReadGameVersion();
            name = reader.ReadString();

            matchmakerToken = null;
            friendCode = null;
            productUserId = null;

            // V1+: LastNonceReceived (uint32) 
            // DTLS 已禁用，此字段总是 0，不再使用
            if (clientVersion >= Version.V1)
            {
                var nonce = reader.ReadUInt32();
                // 不处理 nonce，因为 DTLS 已禁用
            }

            // V2+: 语言 + 聊天模式
            if (clientVersion >= Version.V2)
            {
                language = (Language)reader.ReadUInt32();
                chatMode = (QuickChatModes)reader.ReadByte();
            }
            else
            {
                language = Language.English;
                chatMode = QuickChatModes.FreeChatOrQuickChat;
            }

            // V3+: 平台数据 + ProductUserId + CrossplayFlags
            if (clientVersion >= Version.V3)
            {
                using var platformReader = reader.ReadMessage();
                platformSpecificData = new PlatformSpecificData(platformReader);

                // ★ 关键修改：读取并保留 ProductUserId
                // 这是客户端的 EOS 账号 ID，用于可靠的身份识别
                if (reader.Position < reader.Length)
                {
                    try 
                    { 
                        productUserId = reader.ReadString();
                    } 
                    catch { /* ignore */ }
                }

                // 跳过 CrossplayFlags
                if (reader.Position < reader.Length)
                {
                    try { reader.ReadUInt32(); } catch { /* ignore */ }
                }
            }
            else
            {
                platformSpecificData = null;
            }
        }

        private static class Version
        {
            public static readonly GameVersion V1 = new(2021, 4, 25);
            public static readonly GameVersion V2 = new(2021, 6, 30);
            public static readonly GameVersion V3 = new(2021, 11, 9);
        }
    }
}
