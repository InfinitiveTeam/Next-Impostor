using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Server.GameRecorder;

namespace Impostor.Server.Events.Player
{
    public class PlayerCheckMurderEvent : IPlayerCheckMurderEvent
    {
        public PlayerCheckMurderEvent(IGame game, IClientPlayer clientPlayer, IInnerPlayerControl playerControl, IInnerPlayerControl victim, MurderResultFlags result)
        {
            Game = game;
            ClientPlayer = clientPlayer;
            PlayerControl = playerControl;
            Victim = victim;
            Result = result;

            // 记录玩家检查击杀事件
            string killerName = clientPlayer.Client?.Name ?? "未知玩家";
            string victimName = victim?.PlayerInfo?.PlayerName ?? "未知玩家";
            string resultText = result.ToString();

            var recorder = GameRecorderMain.GetOrCreateRoomRecorder(game.Code.ToString());
            var message = new NanoMessage(NanoMessageType.Common,
                $"击杀检查: {killerName} 试图击杀 {victimName}, 结果: {resultText}");
            recorder.GameData.AppendLine(message.ToString());
        }

        public IGame Game { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }

        public IInnerPlayerControl Victim { get; }

        public MurderResultFlags Result { get; set; }

        public bool IsCancelled { get; set; }
    }
}
