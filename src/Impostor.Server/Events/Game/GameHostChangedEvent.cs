using Impostor.Api.Events;
using Impostor.Api.Games;
using Impostor.Api.Net;
using Impostor.Server.GameRecorder;

namespace Impostor.Server.Events
{
    public class GameHostChangedEvent : IGameHostChangedEvent
    {
        public GameHostChangedEvent(IGame game, IClientPlayer previousHost, IClientPlayer? newHost)
        {
            Game = game;
            PreviousHost = previousHost;
            NewHost = newHost;

            // 记录房主变更事件
            var prevName = previousHost?.Client?.Name ?? "未知";
            var newName = newHost?.Client?.Name ?? "未知";
            GameRecorderMain.GameManagementRecorder.OnGameHostChanged(
                game.Code.ToString(),
                prevName,
                newName);
        }

        public IGame Game { get; }

        public IClientPlayer PreviousHost { get; }

        public IClientPlayer? NewHost { get; }
    }
}
