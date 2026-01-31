using System;
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

            // 记录玩家聊天事件 - 添加空引用检查
            try
            {
                string colorId = playerControl?.PlayerInfo?.CurrentOutfit?.Color.ToString() ?? "未知颜色";
                string playerName = clientPlayer?.Client?.Name ?? "未知玩家";

                GameRecorderMain.MeetingRecorder.OnPlayerChatted(
                    Game.Code.ToString(),
                    colorId,
                    playerName,
                    message);
            }
            catch (Exception ex)
            {
                Program.LogToConsole($"记录聊天事件失败: {ex.Message}", ConsoleColor.Yellow);
            }
        }

        public IGame Game { get; }

        public IClientPlayer ClientPlayer { get; }

        public IInnerPlayerControl PlayerControl { get; }

        public string Message { get; }

        public bool IsCancelled { get; set; }

        public bool SendToAllPlayers { get; set; } = true;
    }
}
