using System.Linq;
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

            GameRecorderMain.PlayerDataRecorder.OnPlayerUpdate(Game.Code, new PlayerDataStore(PlayerControl?.PlayerInfo?.PlayerName, PlayerControl.PlayerInfo.IsImpostor, PlayerControl.PlayerInfo.Tasks.ToArray().Length, PlayerControl.PlayerInfo.IsDead));
        }

        public IGame Game { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }

        public IInnerPlayerControl? Body { get; }
    }
}
