using System.Threading.Tasks;
using Impostor.Api.Games.Managers;
using Impostor.Api.Innersloth;
using Impostor.Api.Innersloth.GameOptions;
using Impostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Impostor.Plugins.Example
{
    [ImpostorPlugin("cl.next.impostor")]
    public class CustomLobby : PluginBase
    {
        private readonly ILogger<CustomLobby> _logger;
        private readonly IGameManager _gameManager;

        public CustomLobby(ILogger<CustomLobby> logger, IGameManager gameManager)
        {
            _logger = logger;
            _gameManager = gameManager;
        }

        public override async ValueTask EnableAsync()
        {
            _logger.LogInformation("【插件】自定义房间已启用");

            var game = await _gameManager.CreateAsync(new NormalGameOptions(), GameFilterOptions.CreateDefault());
            if (game == null)
            {
                _logger.LogWarning("Example game creation was cancelled");
            }
            else
            {
                game.DisplayName = "清风服验证模拟";
                await game.SetPrivacyAsync(true);

                _logger.LogInformation("Created game {0}.", game.Code.Code);
            }
        }

        public override ValueTask DisableAsync()
        {
            _logger.LogInformation("Example is being disabled.");
            return default;
        }
    }
}
