using Impostor.Api.Innersloth;

namespace Impostor.Api.Net.Messages.C2S
{
    public static class HandshakeC2S
    {
        /// <summary>
        /// 解析 UDP 握手包。
        ///
        /// 实际 UDP（非 DTLS）握手包格式（参考 Among Us 客户端及其他项目实现）：
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

            // V3+: 平台数据 (message) + 客户端自报的 ProductUserId (string) + CrossplayFlags (uint32)
            // 注意：顺序是 string 在前，uint32 在后 —— 与之前版本中把 int32 放在 message 后的写法不同！
            if (clientVersion >= Version.V3)
            {
                using var platformReader = reader.ReadMessage();
                platformSpecificData = new PlatformSpecificData(platformReader);

                // 客户端自报的 ProductUserId 字符串（我们从 HTTP 认证缓存中获取，此处跳过）
                if (reader.Position < reader.Length)
                {
                    try { reader.ReadString(); } catch { /* 忽略 */ }
                }

                // CrossplayFlags (uint32)
                if (reader.Position < reader.Length)
                {
                    try { reader.ReadUInt32(); } catch { /* 忽略 */ }
                }
            }
            else
            {
                platformSpecificData = null;
            }

            // UDP 握手包中不含 matchmakerToken / friendCode，
            // 认证完全依赖 HTTP 端点缓存 + IP 匹配（见 ClientManager.RegisterConnectionAsync）。
            matchmakerToken = null;
            friendCode = null;
        }

        private static class Version
        {
            public static readonly GameVersion V1 = new(2021, 4, 25);
            public static readonly GameVersion V2 = new(2021, 6, 30);
            public static readonly GameVersion V3 = new(2021, 11, 9);
        }
    }
}
