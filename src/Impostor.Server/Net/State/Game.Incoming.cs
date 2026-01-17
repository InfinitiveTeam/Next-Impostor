using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Impostor.Hazel;
using Impostor.Server.Events;
using Impostor.Server.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static Impostor.Server.Http.AdminController;

namespace Impostor.Server.Net.State
{
    internal partial class Game
    {
        private readonly SemaphoreSlim _clientAddLock = new SemaphoreSlim(1, 1);

        public async ValueTask HandleStartGame(IMessageReader message)
        {
            GameState = GameStates.Starting;

            using var packet = MessageWriter.Get(MessageType.Reliable);
            message.CopyTo(packet);
            await SendToAllAsync(packet);

            await _eventManager.CallAsync(new GameStartingEvent(this));
        }

        public async ValueTask HandleEndGame(IMessageReader message, GameOverReason gameOverReason)
        {
            GameState = GameStates.Ended;

            // Broadcast end of the game.
            using (var packet = MessageWriter.Get(MessageType.Reliable))
            {
                message.CopyTo(packet);
                await SendToAllAsync(packet);
            }

            // Put all players in the correct limbo state.
            foreach (var player in _players)
            {
                player.Value.Limbo = LimboStates.PreSpawn;
            }

            // Delete all PlayerInfo objects
            foreach (var playerInfo in GameNet.GameData.Players.Values.ToArray())
            {
                await DespawnPlayerInfoAsync(playerInfo);
            }

            await _eventManager.CallAsync(new GameEndedEvent(this, gameOverReason));
        }

        public async ValueTask HandleAlterGame(IMessageReader message, IClientPlayer sender, bool isPublic)
        {
            IsPublic = isPublic;

            using var packet = MessageWriter.Get(MessageType.Reliable);
            message.CopyTo(packet);
            await SendToAllExceptAsync(packet, sender.Client.Id);

            await _eventManager.CallAsync(new GameAlterEvent(this, isPublic));
        }

        public async ValueTask HandleRemovePlayer(int playerId, DisconnectReason reason)
        {
            await PlayerRemove(playerId);

            // It's possible that the last player was removed, so check if the game is still around.
            if (GameState == GameStates.Destroyed)
            {
                return;
            }

            using var packet = MessageWriter.Get(MessageType.Reliable);
            WriteRemovePlayerMessage(packet, false, playerId, reason);
            await SendToAllExceptAsync(packet, playerId);
        }

        public async ValueTask HandleKickPlayer(int playerId, bool isBan)
        {
            _logger.LogInformation("{0} - {1} Left.", Code, playerId);

            using var message = MessageWriter.Get(MessageType.Reliable);

            // Send message to everyone that this player was kicked.
            WriteKickPlayerMessage(message, false, playerId, isBan);

            await SendToAllAsync(message);
            await PlayerRemove(playerId, isBan);

            // Remove the player from everyone's game.
            WriteRemovePlayerMessage(
                message,
                true,
                playerId,
                isBan ? DisconnectReason.Banned : DisconnectReason.Kicked);

            await SendToAllExceptAsync(message, playerId);
        }

        public async ValueTask<GameJoinResult> AddClientAsync(ClientBase client)
        {
            var hasLock = false;

            try
            {
                hasLock = await _clientAddLock.WaitAsync(TimeSpan.FromMinutes(1));

                if (hasLock)
                {
                    return await AddClientSafeAsync(client);
                }
            }
            finally
            {
                if (hasLock)
                {
                    _clientAddLock.Release();
                }
            }

            return GameJoinResult.FromError(GameJoinError.InvalidClient);
        }

        private async ValueTask HandleJoinGameNew(ClientPlayer sender, bool isNew)
        {
            // 检查玩家是否被封禁
            var banService = _serviceProvider.GetRequiredService<BanService>();
            string ipAddress = "未知";
            string location = "未知";

            try
            {
                var endPoint = sender.Client.Connection.EndPoint;
                if (endPoint is IPEndPoint ipEndPoint)
                {
                    ipAddress = ipEndPoint.Address.ToString();

                    // 获取玩家地理位置
                    var httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
                    var logger = _serviceProvider.GetRequiredService<ILogger<AdminController>>();
                    var locationService = new IpLocationService(httpClientFactory.CreateClient(), logger);
                    location = await locationService.GetLocationAsync(ipAddress);
                }
            }
            catch
            {
                // 忽略错误
            }

            _logger.LogInformation("{0}({1})[FriendCode:{2}, Ip:{3}, Location:{4}] Joining in {5}", sender.Client.Name, sender.Client.Id, sender.Client.FriendCode, ipAddress, location, Code);

            // Add player to the game.
            if (isNew)
            {
                await PlayerAdd(sender);
            }

            sender.InitializeSpawnTimeout();

            using (var message = MessageWriter.Get(MessageType.Reliable))
            {
                WriteJoinedGameMessage(message, false, sender);
                WriteAlterGameMessage(message, false, IsPublic);

                sender.Limbo = LimboStates.NotLimbo;

                await SendToAsync(message, sender.Client.Id);
                await BroadcastJoinMessage(message, true, sender);
            }
        }

        private async ValueTask<GameJoinResult> AddClientSafeAsync(ClientBase client)
        {
            // 检查玩家是否被封禁
            var banService = _serviceProvider.GetRequiredService<BanService>();
            string ipAddress = "未知";

            try
            {
                var endPoint = client.Connection.EndPoint;
                if (endPoint is IPEndPoint ipEndPoint)
                {
                    ipAddress = ipEndPoint.Address.ToString();
                }
            }
            catch
            {
                // 忽略错误
            }

            if (banService.IsPlayerBanned(client.FriendCode ?? "", ipAddress))
            {
                _logger.LogWarning("封禁的玩家尝试连接: {PlayerName} (FriendCode: {FriendCode}, IP: {IP})",
                    client.Name, client.FriendCode, ipAddress);
                return GameJoinResult.CreateCustomError("违规游戏行为，已被封禁\n如有疑问请联系服务器所有者");
            }

            // 检查如果玩家的IP被封禁
            if (_bannedIps.Contains(client.Connection.EndPoint.Address))
            {
                return GameJoinResult.FromError(GameJoinError.Banned);
            }

            var player = client.Player;

            // 检查如果玩家运行的版本与主机相同
            if (_compatibilityConfig.AllowVersionMixing == false &&
                this.Host != null && client.GameVersion != this.Host.Client.GameVersion)
            {
                var versionCheckResult = _compatibilityManager.CanJoinGame(Host.Client.GameVersion, client.GameVersion);
                if (versionCheckResult != GameJoinError.None)
                {
                    return GameJoinResult.FromError(versionCheckResult);
                }
            }

            if (GameState == GameStates.Starting || GameState == GameStates.Started)
            {
                return GameJoinResult.FromError(GameJoinError.GameStarted);
            }

            if (GameState == GameStates.Destroyed)
            {
                return GameJoinResult.FromError(GameJoinError.GameDestroyed);
            }

            // 检查：
            // - 玩家是否已经在这个游戏中
            // - 游戏是否已满
            if (player?.Game != this && _players.Count >= Options.MaxPlayers)
            {
                return GameJoinResult.FromError(GameJoinError.GameFull);
            }

            var isNew = false;

            if (player == null || player.Game != this)
            {
                var clientPlayer = new ClientPlayer(_serviceProvider.GetRequiredService<ILogger<ClientPlayer>>(), client, this, _timeoutConfig.SpawnTimeout);

                if (!_clientManager.Validate(client))
                {
                    return GameJoinResult.FromError(GameJoinError.InvalidClient);
                }

                isNew = true;
                player = clientPlayer;
                client.Player = clientPlayer;
            }

            // Check current player state.
            if (player.Limbo == LimboStates.NotLimbo)
            {
                return GameJoinResult.FromError(GameJoinError.InvalidLimbo);
            }

            if (GameState == GameStates.Ended)
            {
                await HandleJoinGameNext(player, isNew);
                return GameJoinResult.CreateSuccess(player);
            }

            var @event = new GamePlayerJoiningEvent(this, player);
            await _eventManager.CallAsync(@event);

            if (@event.JoinResult != null && !@event.JoinResult.Value.IsSuccess)
            {
                return @event.JoinResult.Value;
            }

            await HandleJoinGameNew(player, isNew);
            return GameJoinResult.CreateSuccess(player);
        }

        private async ValueTask HandleJoinGameNext(ClientPlayer sender, bool isNew)
        {
            _logger.LogInformation("{0}({1})[FriendCode:{2}, Ip:{3}] Rejoining in {4}", sender.Client.Name, sender.Client.Id, sender.Client.FriendCode, sender.Client.Connection.EndPoint.Address, Code);

            // Add player to the game.
            if (isNew)
            {
                await PlayerAdd(sender);
            }

            // Check if the host joined and let everyone join.
            if (sender.Client.Id == HostId)
            {
                GameState = GameStates.NotStarted;

                // Spawn the host.
                await HandleJoinGameNew(sender, false);

                // Pull players out of limbo.
                await CheckLimboPlayers();
                return;
            }

            sender.Limbo = LimboStates.WaitingForHost;

            using (var packet = MessageWriter.Get(MessageType.Reliable))
            {
                WriteWaitForHostMessage(packet, false, sender);

                await SendToAsync(packet, sender.Client.Id);
                await BroadcastJoinMessage(packet, true, sender);
            }
        }
    }
}
