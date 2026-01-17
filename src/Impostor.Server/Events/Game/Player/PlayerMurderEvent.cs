using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Server.GameRecorder;

namespace Impostor.Server.Events.Player
{
    public class PlayerMurderEvent : IPlayerMurderEvent
    {
        public PlayerMurderEvent(IGame game, IClientPlayer clientPlayer, IInnerPlayerControl playerControl, IInnerPlayerControl victim, MurderResultFlags result)
        {
            Game = game;
            ClientPlayer = clientPlayer;
            PlayerControl = playerControl;
            Victim = victim;
            Result = result;

            GameRecorderMain.KillRecorder.OnPlayerKilled(PlayerControl.PlayerInfo.CurrentOutfit.Color.ToString(), PlayerControl.PlayerInfo.PlayerName, Victim.PlayerInfo.CurrentOutfit.Color.ToString(), Victim.PlayerInfo.PlayerName);
        }

        public IGame Game { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }

        public IInnerPlayerControl Victim { get; }

        public MurderResultFlags Result { get; }
    }
}
