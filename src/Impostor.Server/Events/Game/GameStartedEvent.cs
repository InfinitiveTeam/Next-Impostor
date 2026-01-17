using Impostor.Api.Events;
using Impostor.Api.Games;
using Impostor.Server.GameRecorder;

namespace Impostor.Server.Events
{
    public class GameStartedEvent : IGameStartedEvent
    {
        public GameStartedEvent(IGame game)
        {
            Game = game;

            GameRecorderMain.GameStateRecorder.OnGameStarted(Game.Code);
        }

        public IGame Game { get; }
    }
}
