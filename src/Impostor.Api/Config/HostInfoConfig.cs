namespace Impostor.Api.Config
{
    public class HostInfoConfig
    {
        public const string Section = "HostInfo";

        public string HostEmail { get; set; } = "example@gmail.com";

        public string AdminUser { get; set; } = "YourAdminUserName";

        public string AdminPassword { get; set; } = "YourAdminUserPassword";
    }
}
