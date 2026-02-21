using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner;
using Impostor.Api.Unity;
using Impostor.Server.Http;
using Impostor.Server.Net.Inner.Objects;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace Impostor.Server.Net.State
{
    internal partial class ClientPlayer : IClientPlayer
    {
        private readonly ILogger<ClientPlayer> _logger;
        private readonly Timer _spawnTimeout;
        private readonly int _spawnTimeoutTime;
        private CancellationTokenSource? _spawnTimeoutCts;

        public ClientPlayer(ILogger<ClientPlayer> logger, ClientBase client, Game game, int timeOutTime)
        {
            _logger = logger;
            _spawnTimeoutTime = timeOutTime;

            Game = game;
            Client = client;
            Limbo = LimboStates.PreSpawn;
        }

        public ClientBase Client { get; }

        public Game Game { get; }

        /// <inheritdoc />
        public LimboStates Limbo { get; set; }

        public InnerPlayerControl? Character { get; internal set; }

        public bool IsHost => Game?.Host == this;

        public string? Scene { get; internal set; }

        public RuntimePlatform? Platform { get; internal set; }

        public void InitializeSpawnTimeout()
        {
            _spawnTimeoutCts?.Cancel();
            _spawnTimeoutCts = new CancellationTokenSource();

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(_spawnTimeoutTime, _spawnTimeoutCts.Token);
                    if (Character == null)
                    {
                        _logger.LogInformation("{0} - Player {1} spawn timed out, kicking.", Game.Code, Client.Id);
                        await KickAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Exception caught while kicking player for spawn timeout.");
                }
            });
        }

        public void DisableSpawnTimeout()
        {
            _spawnTimeoutCts?.Cancel();
        }

        public void Dispose()
        {
            _spawnTimeoutCts?.Cancel();
            _spawnTimeoutCts?.Dispose();
        }

        /// <inheritdoc />
        public bool IsOwner(IInnerNetObject netObject)
        {
            return Client.Id == netObject.OwnerId;
        }

        /// <inheritdoc />
        public ValueTask KickAsync()
        {
            return Game.HandleKickPlayer(Client.Id, false);
        }

        /// <inheritdoc />
        public ValueTask BanAsync()
        {
            return Game.HandleKickPlayer(Client.Id, true);
        }

        private async void RunSpawnTimeout(object state)
        {
            try
            {
                if (Character == null)
                {
                    _logger.LogInformation("{0} - Player {1} spawn timed out, kicking.", Game.Code, Client.Id);

                    await KickAsync();
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception caught while kicking player for spawn timeout.");
            }
            finally
            {
                await _spawnTimeout.DisposeAsync();
            }
        }
    }
}
