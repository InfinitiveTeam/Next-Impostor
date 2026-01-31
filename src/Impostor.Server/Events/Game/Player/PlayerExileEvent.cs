using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Server.GameRecorder;

namespace Impostor.Server.Events.Player
{
    public class PlayerExileEvent : IPlayerExileEvent
    {
        public PlayerExileEvent(IGame game, IClientPlayer clientPlayer, IInnerPlayerControl playerControl)
        {
            Game = game;
            ClientPlayer = clientPlayer;
            PlayerControl = playerControl;

            GameRecorderMain.MeetingRecorder.OnPlayerExile(Game.Code, PlayerControl.PlayerInfo.PlayerName,CheckType(PlayerControl));
        }
        string CheckType(IInnerPlayerControl PlayerControl)
        {
            if (PlayerControl.PlayerInfo.IsImpostor) return "他是伪装者";
            else if (!PlayerControl.PlayerInfo.IsImpostor) return "他不是伪装者";
            return "他的身份未知。可能是房主未开启驱逐确认玩家身份";
        }

        public IGame Game { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }
    }
}
