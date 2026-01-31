using Impostor.Api.Events;
using Impostor.Api.Games;
using Impostor.Api.Net;
using Impostor.Server.GameRecorder;

namespace Impostor.Server.Events
{
    public class GamePlayerJoinedEvent : IGamePlayerJoinedEvent
    {
        public GamePlayerJoinedEvent(IGame game, IClientPlayer player)
        {
            Game = game;
            Player = player;

            // 记录玩家加入事件
            var playerName = player.Client?.Name ?? "未知玩家";
            GameRecorderMain.PlayerRecorder.OnPlayerJoined(game.Code.ToString(), playerName);
        }

        public IGame Game { get; }

        public IClientPlayer Player { get; }
    }
}
