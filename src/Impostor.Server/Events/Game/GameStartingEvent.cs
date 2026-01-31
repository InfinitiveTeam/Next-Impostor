using Impostor.Api.Events;
using Impostor.Api.Games;
using Impostor.Server.GameRecorder;

namespace Impostor.Server.Events
{
    public class GameStartingEvent : IGameStartingEvent
    {
        public GameStartingEvent(IGame game)
        {
            Game = game;

            // 记录游戏即将开始事件
            GameRecorderMain.GameManagementRecorder.OnGameStarting(Game.Code.ToString());
        }

        public IGame Game { get; }
    }
}
