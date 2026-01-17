using System.Reflection;
using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Server.GameRecorder;

namespace Impostor.Server.Events.Player
{
    public class PlayerChatEvent : IPlayerChatEvent
    {
        public PlayerChatEvent(IGame game, IClientPlayer clientPlayer, IInnerPlayerControl playerControl, string message)
        {
            Game = game;
            ClientPlayer = clientPlayer;
            PlayerControl = playerControl;
            Message = message;

            GameRecorderMain.MeetingRecorder.OnPlayerChatted(PlayerControl.PlayerInfo?.CurrentOutfit.Color.ToString(), clientPlayer.Client.Name, message);
        }

        public IGame Game { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }

        public string Message { get; }

        public bool IsCancelled { get; set; }
    }
}
