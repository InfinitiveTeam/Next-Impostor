using System;
using Impostor.Api.Innersloth;
using Serilog;

namespace Impostor.Api.Net.Messages.C2S
{
    public static class HandshakeC2S
    {
        /// <summary>
        /// 解析 UDP 握手包，提取包含 matchmakerToken（即服务端签发的 base64 token）和 friendCode。
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

            // Among Us 2021.4.25+: lastNonceReceived (Impostor re-purposes this field as matchmakerToken index)
            // 参考项目读法：UDP 非 DTLS 下此字段是 uint lastId（对应我们 token 里的 lastId/nonce）
            // 但我们的 token 方案不使用 lastId，而是通过 matchmakerToken 字符串携带认证。
            // Among Us 客户端在握手时发送的是整个 base64 token 字符串放在原先的 "crossplayFlags" 区域之后。
            // 实际协议：V3+ 握手末尾有两个字段：一个字符串（matchmakerToken）和一个 uint32（crossplayFlags）。
            if (clientVersion >= Version.V1)
            {
                reader.ReadUInt32(); // lastNonceReceived / lastId（不使用）
            }

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

            if (clientVersion >= Version.V3)
            {
                using var platformReader = reader.ReadMessage();
                platformSpecificData = new PlatformSpecificData(platformReader);
                reader.ReadInt32(); // crossplayFlags, not used yet
            }
            else
            {
                platformSpecificData = null;
            }

            if (clientVersion >= Version.V4)
            {
                reader.ReadByte(); // purpose unknown, seems hardcoded to 0
            }

            // V5+: matchmakerToken 和 friendCode 字段
            // 客户端会在握手末尾附加这两个字符串
            matchmakerToken = null;
            friendCode = null;
            if (clientVersion >= Version.V5)
            {
                try
                {
                    if (reader.Position < reader.Length)
                    {
                        matchmakerToken = reader.ReadString();
                    }

                    if (reader.Position < reader.Length)
                    {
                        friendCode = reader.ReadString();
                    }
                }
                catch (Exception)
                {
                    // 老客户端可能不发送这些字段，忽略异常
                }
            }
        }

        private static class Version
        {
            public static readonly GameVersion V1 = new(2021, 4, 25);

            public static readonly GameVersion V2 = new(2021, 6, 30);

            public static readonly GameVersion V3 = new(2021, 11, 9);

            public static readonly GameVersion V4 = new(2021, 12, 14);

            // V5: 引入 matchmakerToken 和 friendCode 字段（2022.x+）
            public static readonly GameVersion V5 = new(2022, 3, 28);
        }
    }
}
