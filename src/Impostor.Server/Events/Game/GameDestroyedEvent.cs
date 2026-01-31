using Impostor.Api.Events;
using Impostor.Api.Games;
using Impostor.Server.GameRecorder;
using Impostor.Server.Http;

namespace Impostor.Server.Events
{
    public class GameDestroyedEvent : IGameDestroyedEvent
    {
        public GameDestroyedEvent(IGame game)
        {
            Game = game;
            AdminController.OnRoomClosed(game.Code);

            GameRecorderMain.GameManagementRecorder.OnGameDestroyed(game.Code.ToString());

            GameRecorderMain.ClearData(game.Code.ToString());
            if (game is Impostor.Server.Net.State.Game serverGame)
            {
                serverGame.DeepSeekText = null;
                serverGame.SendDeepSeekText = null;
            }
        }

        public IGame Game { get; }
    }
}
