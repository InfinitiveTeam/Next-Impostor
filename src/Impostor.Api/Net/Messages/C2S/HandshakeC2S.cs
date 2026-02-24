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
        ///   [V1+] LastNonceReceived (uint32) — 从 DTLS 认证端口收到的 nonce，用于无 IP 认证
        ///   [V2+] Language (uint32) + ChatMode (byte)
        ///   [V3+] PlatformSpecificData (message) + ProductUserId (string) + CrossplayFlags (uint32)
        ///
        /// 认证流程（无 IP 依赖）：
        ///   1. HTTP POST /api/user → 获得 matchmakerToken
        ///   2. DTLS port+2 → 发送 matchmakerToken + FriendCode → 服务端回复 nonce
        ///   3. UDP game port → 握手中 LastNonceReceived 字段带回 nonce
        ///   4. 服务端通过 nonce 查找 FriendCode
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

            // V1+: LastNonceReceived (uint32) — 客户端从 DTLS 认证端口收到后原样带回
            // 用 "NONCE:" 前缀包装，让 ClientManager 识别并通过 nonce 查找 FriendCode
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

            // V3+: 平台数据 + ProductUserId + CrossplayFlags
            // 不读取额外字节，避免误解析 Reactor 等 Mod 附加数据（之前版本把 "ro" 读成 token）
            if (clientVersion >= Version.V3)
            {
                using var platformReader = reader.ReadMessage();
                platformSpecificData = new PlatformSpecificData(platformReader);

                // 跳过 ProductUserId（客户端自报，服务端不信任）
                if (reader.Position < reader.Length)
                {
                    try { reader.ReadString(); } catch { /* ignore */ }
                }

                // 跳过 CrossplayFlags
                if (reader.Position < reader.Length)
                {
                    try { reader.ReadUInt32(); } catch { /* ignore */ }
                }

                // 不再尝试读取额外字节：
                // - 标准客户端此处无额外数据
                // - Reactor 等 Mod 在此处附加 mod 标识，不应被当作认证数据
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
