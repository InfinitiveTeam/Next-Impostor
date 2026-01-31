using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Server.GameRecorder;

namespace Impostor.Server.Events.Player
{
    public class PlayerCompletedTaskEvent : IPlayerCompletedTaskEvent
    {
        public PlayerCompletedTaskEvent(IGame game, IClientPlayer clientPlayer, IInnerPlayerControl playerControl, ITaskInfo task)
        {
            Game = game;
            ClientPlayer = clientPlayer;
            PlayerControl = playerControl;
            Task = task;

            // 记录玩家完成任务事件
            string playerName = clientPlayer.Client?.Name ?? "未知玩家";
            string taskName = task?.Task?.Type.ToString() ?? "未知任务";
            GameRecorderMain.PlayerRecorder.OnPlayerCompletedTask(
                game.Code.ToString(),
                playerName,
                taskName);
        }

        public IGame Game { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }

        public ITaskInfo Task { get; }
    }
}
