using Impostor.Api.Utils;

namespace Impostor.Api.Config
{
    public class ServerConfig
    {
        public const string Section = "Server";

        public string PublicIp { get; set; } = "127.0.0.1";

        public ushort PublicPort { get; set; } = 22023;

        public string ListenIp { get; set; } = "0.0.0.0";

        public ushort ListenPort { get; set; } = 22023;

        /// <summary>
        /// 解析对外地址。为了支持 DDNS，这里每次调用都会重新解析域名。
        /// </summary>
        public string ResolvePublicIp()
        {
            return IpUtils.ResolveIp(PublicIp);
        }

        public string ResolveListenIp()
        {
            return IpUtils.ResolveIp(ListenIp);
        }
    }
}
