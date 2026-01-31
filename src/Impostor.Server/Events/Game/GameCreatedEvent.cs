using Impostor.Api.Events;
using Impostor.Api.Games;
using Impostor.Api.Net;
using Impostor.Server.GameRecorder;

namespace Impostor.Server.Events
{
    public class GameCreatedEvent : IGameCreatedEvent
    {
        public GameCreatedEvent(IGame game, IClient? host)
        {
            Game = game;
            Host = host;

            var hostName = host?.Name ?? "未知";
            GameRecorderMain.GameManagementRecorder.OnGameCreated(game.Code.ToString(), hostName);
        }

        public IGame Game { get; }

        public IClient? Host { get; }
    }
}
