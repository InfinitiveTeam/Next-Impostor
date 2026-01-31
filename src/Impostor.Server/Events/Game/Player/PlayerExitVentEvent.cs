using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Innersloth.Maps;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Server.GameRecorder;

namespace Impostor.Server.Events.Player
{
    public class PlayerExitVentEvent : IPlayerExitVentEvent
    {
        public PlayerExitVentEvent(IGame game, IClientPlayer sender, IInnerPlayerControl innerPlayerPhysics, VentData vent)
        {
            Game = game;
            ClientPlayer = sender;
            PlayerControl = innerPlayerPhysics;
            Vent = vent;

            // 记录玩家离开通风管事件
            string playerName = sender.Client?.Name ?? "未知玩家";
            string ventName = vent?.Name ?? "未知通风管";
            GameRecorderMain.PlayerRecorder.OnPlayerExitVent(
                game.Code.ToString(),
                playerName,
                ventName);
        }

        public IGame Game { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }

        public VentData Vent { get; }
    }
}
