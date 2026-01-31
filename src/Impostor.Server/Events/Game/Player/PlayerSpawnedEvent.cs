using System;
using System.IO;
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

            // 记录玩家生成事件
            string playerName = clientPlayer.Client?.Name ?? "未知玩家";
            GameRecorderMain.PlayerRecorder.OnPlayerSpawned(game.Code.ToString(), playerName);
        }

        public IGame Game { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }
    }
}
