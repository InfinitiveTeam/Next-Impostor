using System.Text.Json;
using System.Threading.Tasks;
using Impostor.Api.Events.Managers;
using Impostor.Api.Innersloth.GameOptions;
using Impostor.Server.Events;
using Impostor.Server.GameRecorder;
using Impostor.Server.Net.State;

namespace Impostor.Server.Net.Inner.Objects.GameManager.Logic;

internal abstract class LogicOptions : GameLogicComponent
{
    private readonly Game _game;
    private readonly IEventManager _eventManager;

    protected LogicOptions(Game game, IEventManager eventManager)
    {
        _game = game;
        _eventManager = eventManager;
    }

    public override ValueTask<bool> SerializeAsync(IMessageWriter writer, bool initialState)
    {
        GameOptionsFactory.Serialize(writer, _game.Options);
        return ValueTask.FromResult(true);
    }

    public override async ValueTask DeserializeAsync(IMessageReader reader, bool initialState)
    {
        GameOptionsFactory.DeserializeInto(reader, _game.Options);

        // 调用游戏选项记录接口
        // string optionsJson = JsonSerializer.Serialize(_game.Options); // JSON格式
        string optionsText = _game.Options.ToString(); // ToString方法

        GameRecorderMain.AllOptionsRecorder.OnGameOptionsLoaded(_game.Code, optionsText);

        await _eventManager.CallAsync(new GameOptionsChangedEvent(
            _game,
            Api.Events.IGameOptionsChangedEvent.ChangeReason.Host
        ));
    }
}
