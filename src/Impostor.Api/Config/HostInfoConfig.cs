namespace Impostor.Api.Config
{
    public class HostInfoConfig
    {
        public const string Section = "HostInfo";

        public string HostEmail { get; set; } = "example@gmail.com";

        public string AdminUser { get; set; } = "YourAdminUserName";

        public string AdminPassword { get; set; } = "YourAdminUserPassword";

        public string SmtpHost { get; set; } = "smtp.qq.com";

        public int SmtpPort { get; set; } = 587;

        public string Username { get; set; } = "example @gameil.com";

        public string Password { get; set; } = "baidufgggfs";

        public string FromEmail { get; set; } = "example @gameil.com";

        public string DeepSeekAPIKey { get; set; } = "emampleapikey";
    }
}
