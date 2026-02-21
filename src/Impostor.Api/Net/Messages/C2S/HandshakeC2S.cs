using System;
using Impostor.Api.Innersloth;

namespace Impostor.Api.Net.Messages.C2S
{
    public static class HandshakeC2S
    {
        /// <summary>
        /// 解析 UDP 握手包。
        ///
        /// 实际 UDP（非 DTLS）握手包格式（参考 Among Us 客户端及 preview 项目实现）：
        ///   GameVersion (4B)
        ///   Name (string)
        ///   [V1+] LastNonce / uint32 (4B) — 不使用，忽略
        ///   [V2+] Language (uint32) + ChatMode (byte)
        ///   [V3+] PlatformSpecificData (message) + ProductUserId (string, 客户端自报) + CrossplayFlags (uint32)
        ///
        /// 注意：UDP 握手包中不包含 matchmakerToken 或 friendCode。
        /// 认证信息通过 HTTP /api/user 端点预先交换，并在服务端按 IP 地址缓存关联。
        /// </summary>
        public static void Deserialize(
            IMessageReader reader,
            out GameVersion clientVersion,
            out string name,
            out Language language,
            out QuickChatModes chatMode,
            out PlatformSpecificData? platformSpecificData,
            out string? matchmakerToken,
            out string? friendCode)
        {
            clientVersion = reader.ReadGameVersion();
            name = reader.ReadString();

            // V1+: lastNonce / lastId（uint32）— 客户端用于重连，服务端忽略
            if (clientVersion >= Version.V1)
            {
                reader.ReadUInt32();
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

            // V3+: 平台数据 (message) + ProductUserId/matchmakerToken (string) + CrossplayFlags (uint32)
            matchmakerToken = null;
            friendCode = null;
            if (clientVersion >= Version.V3)
            {
                using var platformReader = reader.ReadMessage();
                platformSpecificData = new PlatformSpecificData(platformReader);

                // 读取 ProductUserId 字符串位置的数据：
                // - 标准客户端：此处为 ProductUserId（跳过）
                // - 自定义客户端：此处可能携带 matchmakerToken（base64 JSON，以 'ey' 开头）
                string? productUserIdOrToken = null;
                if (reader.Position < reader.Length)
                {
                    try { productUserIdOrToken = reader.ReadString(); } catch { /* 忽略 */ }
                }

                // CrossplayFlags (uint32)
                if (reader.Position < reader.Length)
                {
                    try { reader.ReadUInt32(); } catch { /* 忽略 */ }
                }

                // 尝试从剩余字节读取附加的 matchmakerToken 和 friendCode（自定义客户端扩展）
                if (reader.Position < reader.Length)
                {
                    try { matchmakerToken = reader.ReadString(); } catch { /* 忽略 */ }
                }
                if (reader.Position < reader.Length)
                {
                    try { friendCode = reader.ReadString(); } catch { /* 忽略 */ }
                }

                // 如果附加字段为空，检查 productUserIdOrToken 是否为 base64 JSON token
                // （自定义客户端可能把 matchmakerToken 放在 ProductUserId 位置）
                if (matchmakerToken == null
                    && productUserIdOrToken != null
                    && productUserIdOrToken.Length > 10
                    && productUserIdOrToken.StartsWith("ey", System.StringComparison.Ordinal))
                {
                    matchmakerToken = productUserIdOrToken;
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
