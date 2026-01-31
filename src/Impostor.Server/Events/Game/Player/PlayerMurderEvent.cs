using System;
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

            // 记录击杀事件 - 添加空引用检查
            try
            {
                string killerColor = playerControl?.PlayerInfo?.CurrentOutfit?.Color.ToString() ?? "未知颜色";
                string killerName = playerControl?.PlayerInfo?.PlayerName ?? "未知玩家";
                string victimColor = victim?.PlayerInfo?.CurrentOutfit?.Color.ToString() ?? "未知颜色";
                string victimName = victim?.PlayerInfo?.PlayerName ?? "未知玩家";

                GameRecorderMain.KillRecorder.OnPlayerKilled(
                    Game.Code.ToString(),
                    killerColor,
                    killerName,
                    victimColor,
                    victimName);
            }
            catch (Exception ex)
            {
                Program.LogToConsole($"记录击杀事件失败: {ex.Message}", ConsoleColor.Yellow);
            }
        }

        public IGame Game { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }

        public IInnerPlayerControl Victim { get; }

        public MurderResultFlags Result { get; }
    }
}
