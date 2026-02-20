using Impostor.Api.Innersloth;

namespace Impostor.Api.Net.Messages.C2S
{
    /// <summary>
    /// 解析 DTLS 认证握手包（客户端连接认证端口 port+2 时发送）。
    /// 格式：GameVersion(4B), Platform(1B), MatchmakerToken(string), FriendCode(string)
    /// </summary>
    public static class AuthHandshakeC2S
    {
        public static void Deserialize(
            IMessageReader reader,
            out GameVersion clientVersion,
            out string matchmakerToken,
            out string friendCode)
        {
            clientVersion = reader.ReadGameVersion();
            _ = reader.ReadByte(); // Platform (Platforms enum)
            matchmakerToken = reader.ReadString();
            friendCode = reader.ReadString();
        }
    }
}
