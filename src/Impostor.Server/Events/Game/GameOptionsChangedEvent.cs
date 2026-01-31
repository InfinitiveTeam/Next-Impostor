using Impostor.Api.Events;
using Impostor.Api.Games;
using Impostor.Server.GameRecorder;
using static Impostor.Api.Events.IGameOptionsChangedEvent;

namespace Impostor.Server.Events
{
    public class GameOptionsChangedEvent : IGameOptionsChangedEvent
    {
        public GameOptionsChangedEvent(IGame game, ChangeReason changedBy)
        {
            Game = game;
            ChangedBy = changedBy;

            // 记录游戏选项更改事件
            GameRecorderMain.GameManagementRecorder.OnGameOptionsChanged(
                game.Code.ToString(),
                changedBy.ToString());
        }

        public ChangeReason ChangedBy { get; }

        public IGame Game { get; }
    }
}
