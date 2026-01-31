using Impostor.Api.Events;
using Impostor.Api.Games;
using Impostor.Api.Net;
using Impostor.Server.GameRecorder;

namespace Impostor.Server.Events
{
    public class GamePlayerLeftEvent : IGamePlayerLeftEvent
    {
        public GamePlayerLeftEvent(IGame game, IClientPlayer player, bool isBan)
        {
            Game = game;
            Player = player;
            IsBan = isBan;

            // 记录玩家离开事件
            string playerName = player.Client?.Name ?? "未知玩家";
            GameRecorderMain.PlayerRecorder.OnPlayerLeft(game.Code.ToString(), playerName, isBan);
        }

        public IGame Game { get; }

        public IClientPlayer Player { get; }

        public bool IsBan { get; }
    }
}
