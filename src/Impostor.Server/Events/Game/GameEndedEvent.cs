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

            // 修复：将IGame转换为具体的Game类型
            var serverGame = game as Impostor.Server.Net.State.Game;
            if (serverGame != null)
            {
                GameRecorderMain.GameStateRecorder.OnGameEnded(Game.Code.ToString(), serverGame);
            }
            else
            {
                // 如果无法转换，至少传递房间代码
                GameRecorderMain.GameStateRecorder.OnGameEnded(Game.Code.ToString(), null);
            }
        }

        public IGame Game { get; }

        public GameOverReason GameOverReason { get; }
    }
}
