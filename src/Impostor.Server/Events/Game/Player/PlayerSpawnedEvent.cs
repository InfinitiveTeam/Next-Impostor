using System;
using System.Data;
using System.IO;
using System.Linq;
using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Server.GameRecorder;

namespace Impostor.Server.Events.Player
{
    public class PlayerSpawnedEvent : IPlayerSpawnedEvent
    {
        public PlayerSpawnedEvent(IGame game, IClientPlayer clientPlayer, IInnerPlayerControl playerControl)
        {
            Game = game;
            ClientPlayer = clientPlayer;
            PlayerControl = playerControl;

            GameRecorderMain.PlayerDataRecorder.OnPlayerUpdate(Game.Code, new PlayerDataStore(PlayerControl?.PlayerInfo?.PlayerName, PlayerControl.PlayerInfo.IsImpostor, PlayerControl.PlayerInfo.Tasks.ToArray().Length, PlayerControl.PlayerInfo.IsDead));
        }

        public IGame Game { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }
    }
}
