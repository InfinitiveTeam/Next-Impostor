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
        ///   [V1+] LastNonceReceived (uint32) 或 matchmakerToken (string) — DTLS or NonDTLS
        ///   [V2+] Language (uint32) + ChatMode (byte)
        ///   [V3+] PlatformSpecificData (message)
        ///   [V3+] FriendCode (string) 或 ProductUserId (string) — DTLS or NonDTLS
        ///   [V3+] CrossplayFlags (uint32)
        ///
        /// 认证流程：
        ///   - useDtlsLayout=true: 握手包中发送 matchmakerToken + FriendCode
        ///   - useDtlsLayout=false: 握手包中发送 Nonce(uint32) + FriendCode(实际上被客户端设置为空)
        ///
        /// ★ 关键修改：在非DTLS模式下，FriendCode 字段现在会被读取（之前被忽略）
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

            // V1+: LastNonceReceived (uint32) 或 matchmakerToken (string)
            // 根据客户端的 useDtlsLayout 设置，此处读取不同类型数据
            // 在 Among Us 客户端：
            //   if (useDtlsLayout) -> Write(matchmakerToken)
            //   else -> Write(LastNonceReceived.GetValueOrDefault())
            // 由于我们无法从握手中判断 useDtlsLayout，两种情况都尝试：
            if (clientVersion >= Version.V1)
            {
                // 尝试读取为 uint32 (Nonce)
                var startPos = reader.Position;
                try
                {
                    var nonce = reader.ReadUInt32();
                    if (nonce != 0)
                    {
                        matchmakerToken = $"NONCE:{nonce}";
                    }
                }
                catch
                {
                    // 如果读取 uint32 失败，说明这是 matchmakerToken (string)
                    reader.Position = startPos;
                    matchmakerToken = reader.ReadString();
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

            // V3+: FriendCode (string) 或 ProductUserId (string)
            // ★ 关键改动：现在 DTLS 禁用时仍然读取 FriendCode
            // 在 Among Us 客户端：
            //   if (useDtlsLayout) -> Write(FriendCode)
            //   else -> Write("") 和 Write(0U)
            // 
            // 但我们会尽量读取，以支持客户端修改版本，以及未来可能的改动
            if (clientVersion >= Version.V3)
            {
                // ★ 关键：现在读取 FriendCode，即使在非 DTLS 模式
                // 服务端应该修改 AU 客户端，在非 DTLS 模式下也发送 FriendCode
                // 或者在握手前通过 HTTP 认证缓存 FriendCode
                if (reader.Position < reader.Length)
                {
                    try
                    {
                        friendCode = reader.ReadString();
                        // 如果读取的是空字符串，视为 null
                        if (string.IsNullOrEmpty(friendCode))
                        {
                            friendCode = null;
                        }
                    }
                    catch
                    {
                        // 忽略读取错误
                    }
                }

                // 跳过 CrossplayFlags
                if (reader.Position < reader.Length)
                {
                    try { reader.ReadUInt32(); } catch { /* ignore */ }
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
