using Impostor.Api.Events;
using Impostor.Api.Games;
using Impostor.Server.GameRecorder;

namespace Impostor.Server.Events
{
    public class GameAlterEvent : IGameAlterEvent
    {
        public GameAlterEvent(IGame game, bool isPublic)
        {
            Game = game;
            IsPublic = isPublic;

            // 记录游戏修改事件
            GameRecorderMain.GameManagementRecorder.OnGameAlter(game.Code.ToString(), isPublic);
        }

        public IGame Game { get; }

        public bool IsPublic { get; }
    }
}
