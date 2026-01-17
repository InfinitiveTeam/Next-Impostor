using System.Reflection;
using Impostor.Api.Events;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Server.GameRecorder;

namespace Impostor.Server.Events
{
    public class GameEndedEvent : IGameEndedEvent
    {
        public GameEndedEvent(IGame game, GameOverReason gameOverReason)
        {
            Game = game;
            GameOverReason = gameOverReason;

            GameRecorderMain.GameStateRecorder.OnGameEnded(Game.Code);
        }

        public IGame Game { get; }

        public GameOverReason GameOverReason { get; }
    }
}
