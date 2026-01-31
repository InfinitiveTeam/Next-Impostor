using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Server.GameRecorder;

namespace Impostor.Server.Events.Player
{
    public class PlayerStartMeetingEvent : IPlayerStartMeetingEvent
    {
        public PlayerStartMeetingEvent(IGame game, IClientPlayer clientPlayer, IInnerPlayerControl playerControl, IInnerPlayerControl? body)
        {
            Game = game;
            ClientPlayer = clientPlayer;
            PlayerControl = playerControl;
            Body = body;

            // 记录玩家发起会议事件
            string playerName = clientPlayer.Client?.Name ?? "未知玩家";
            string bodyName = body?.PlayerInfo?.PlayerName ?? "无尸体";
            var recorder = GameRecorderMain.GetOrCreateRoomRecorder(game.Code.ToString());
            var message = new NanoMessage(NanoMessageType.Meeting, $"玩家 {playerName} 发起会议，尸体: {bodyName}");
            recorder.GameData.AppendLine(message.ToString());
        }

        public IGame Game { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }

        public IInnerPlayerControl? Body { get; }
    }
}
