using System;
using Impostor.Api.Innersloth;

namespace Impostor.Api.Net.Messages.C2S
{
    public static class HandshakeC2S
    {
        /// <summary>
        /// 解析标准 UDP 握手包。
        ///
        /// 格式（Among Us 客户端 GetConnectionData）：
        ///   GameVersion (4B)
        ///   Name (string)
        ///   [V1+] LastNonceReceived (uint32) 或 matchmakerToken — 取决于 useDtls 模式
        ///   [V2+] Language (uint32) + ChatMode (byte)
        ///   [V3+] PlatformSpecificData (message)
        ///   [V3+] FriendCode (string) 或 ProductUserId (string) — 可选，取决于 useDtls 模式
        ///   [V3+] CrossplayFlags (uint32) — 可选
        ///
        /// ★ 改动：在所有可读取的地方都尝试读取字段，即使可能失败
        /// 这样可以支持两种握手格式（DTLS 和 非DTLS）
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

            matchmakerToken = null;
            friendCode = null;

            // V1+: LastNonceReceived (uint32)
            // 在非DTLS模式：uint32，通常为 0
            // 在DTLS模式：这里是 matchmakerToken (string)，但已被 mods 改为 uint32
            if (clientVersion >= Version.V1)
            {
                var nonce = reader.ReadUInt32();
                if (nonce != 0)
                {
                    matchmakerToken = $"NONCE:{nonce}";
                }
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

            // V3+: 平台数据
            if (clientVersion >= Version.V3)
            {
                using var platformReader = reader.ReadMessage();
                platformSpecificData = new PlatformSpecificData(platformReader);
            }
            else
            {
                platformSpecificData = null;
            }

            // V3+: 尝试读取剩余字段（可能失败）
            if (clientVersion >= Version.V3)
            {
                try
                {
                    // 尝试读取 FriendCode (string)
                    // 在DTLS模式中存在，在非DTLS模式中为空字符串
                    friendCode = reader.ReadString();
                    if (string.IsNullOrEmpty(friendCode))
                    {
                        friendCode = null;
                    }
                }
                catch
                {
                    // 如果读取失败（数据不足），friendCode 保持 null
                    friendCode = null;
                }

                try
                {
                    // 尝试读取 ProductUserId (string) 或 CrossplayFlags (uint32)
                    // 这些字段在非DTLS 且无DTLS层时不存在
                    reader.ReadString();
                }
                catch
                {
                    // 忽略读取失败
                }

                try
                {
                    // 尝试读取 CrossplayFlags (uint32)
                    reader.ReadUInt32();
                }
                catch
                {
                    // 忽略读取失败
                }
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
