namespace Impostor.Api.Config
{
    public static class DisconnectMessages
    {
        public const string Error = "An internal server error occurred, please contact an administrator/developer";
        public const string ClientOutdated = "Please update your game to join this lobby";
        public const string ClientTooNew = "Your game version is too new for this lobby. If you want to join this lobby, please downgrade your client version";
        public const string Destroyed = "The game you tried to join is being destroyed. Please create a new game";
        public const string UsernameLength = "Username is too long, please shorten it";
        public const string UsernameIllegalCharacters = "Username contains illegal characters, please remove them";
        public const string VersionClientTooOld = "Please update your game to connect to this server";
        public const string VersionServerTooOld = "Error: Unsupported version [1]";
        public const string VersionUnsupported = "Error: Unsupported version [2]";

        private const string UpgradingDocsLink = "https://github.com/Impostor/Impostor/blob/master/docs/Upgrading.md";
        public const string UdpMatchmakingUnsupported = $"""
                                                  Sorry, UDP matchmaking is no longer supported.
                                                  Please refer to <link={UpgradingDocsLink}#impostor-190>Impostor documentation</link> for how to migrate to HTTP matchmaking
                                                 """;

        public const string HostAuthorityUnsupported = "Your client requested host authority [+25 protocol], but this NImpostor server does not have this feature enabled.";

        public const string ClientInvalidState = "Client is in an invalid state.";
        public const string InvalidLimboState = "Invalid limbo state while joining.";
        public const string UnknownError = "Unknown error.";
        public const string CheatingKicked = "You have been caught cheating and were kicked from the lobby. For questions, contact your server admin and share the following code: {0}.";
        public const string CheatingBanned = "You have been caught cheating and were banned from the lobby. For questions, contact your server admin and share the following code: {0}.";
        public const string GameNotFound = "Game not found.";
        public const string GameFull = "Game is full.";
        public const string GameStarted = "Game has already started.";
        public const string Banned = "You are banned from this game.";
    }
}
