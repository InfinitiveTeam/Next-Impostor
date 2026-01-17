namespace Impostor.Api.Config
{
    public static class DisconnectMessages
    {
        public const string Error = "发生内部服务器错误,请联系管理/开发人员";

        public const string ClientOutdated = "请更新游戏以加入该大厅";

        public const string ClientTooNew = "您的游戏版本对此大厅来说过新" +
                                           "如需加入该大厅，请降级您的客户端版本";

        public const string Destroyed = "您尝试加入的游戏正在被销毁" +
                                        "请创建新游戏";

        public const string UsernameLength = "用户名过长，请缩短用户名";

        public const string UsernameIllegalCharacters = "用户名包含非法字符，请移除这些字符";

        public const string VersionClientTooOld = "请更新游戏以连接此服务器";

        public const string VersionServerTooOld = "Error: 不支持的版本 [1]";

        public const string VersionUnsupported = "Error: 不支持的版本 [2]";

        private const string UpgradingDocsLink = "https://github.com/Impostor/Impostor/blob/master/docs/Upgrading.md";

        public const string UdpMatchmakingUnsupported = $"""
                                                 很抱歉，UDP 匹配已不再受支持。
                                                 请参阅<link={UpgradingDocsLink}#impostor-190>Impostor 文档</link>了解如何迁移至 HTTP 匹配
                                                 """;

        public const string HostAuthorityUnsupported = "您的客户端请求主机权限[+25协议]，但此 NImpostor 服务器未启用该功能。";
    }
}
