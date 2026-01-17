using Impostor.Api.Events;
using Impostor.Api.Games;
using Impostor.Server.Http;

namespace Impostor.Server.Events
{
    public class GameDestroyedEvent : IGameDestroyedEvent
    {
        public GameDestroyedEvent(IGame game)
        {
            Game = game;
            AdminController.OnRoomClosed(game.Code);
        }

        public IGame Game { get; }
    }
}
