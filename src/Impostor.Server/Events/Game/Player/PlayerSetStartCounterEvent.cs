using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Server.GameRecorder;

namespace Impostor.Server.Events.Player
{
    public class PlayerSetStartCounterEvent : IPlayerSetStartCounterEvent
    {
        public PlayerSetStartCounterEvent(IGame game, IClientPlayer clientPlayer, IInnerPlayerControl playerControl, byte secondsLeft)
        {
            Game = game;
            ClientPlayer = clientPlayer;
            PlayerControl = playerControl;
            SecondsLeft = secondsLeft;

            // 记录游戏开始倒计时事件
            var recorder = GameRecorderMain.GetOrCreateRoomRecorder(game.Code.ToString());
            var message = new NanoMessage(NanoMessageType.GameState, $"游戏开始倒计时: {secondsLeft}秒");
            recorder.GameData.AppendLine(message.ToString());
        }

        public byte SecondsLeft { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }

        public IGame Game { get; }
    }
}
