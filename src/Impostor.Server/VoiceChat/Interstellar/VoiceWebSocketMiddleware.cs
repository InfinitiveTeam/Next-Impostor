using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.VoiceChat.Interstellar;

internal sealed class VoiceWebSocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<VoiceWebSocketMiddleware> _logger;

    public VoiceWebSocketMiddleware(RequestDelegate next, ILogger<VoiceWebSocketMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, VoiceRoomManager roomManager)
    {
        if (!context.Request.Path.Equals("/vc", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            await context.Response.WriteAsync("This endpoint is WebSocket only. Use ws(s)://.../vc");
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        await HandleSocketAsync(socket, roomManager, context.RequestAborted);
    }

    private async Task HandleSocketAsync(WebSocket socket, VoiceRoomManager roomManager, CancellationToken cancellationToken)
    {
        VoiceRoom? room = null;
        VoiceSession? session = null;

        try
        {
            var join = await ReceiveJoinAsync(socket, cancellationToken);
            if (join == null)
            {
                await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "join required", cancellationToken);
                return;
            }

            room = roomManager.GetRoom(join.Region, join.RoomCode);
            session = new VoiceSession(room.NextClientId(), socket, _logger);
            session.UpdateProfile(join.PlayerName, join.PlayerId);
            room.Join(session);

            await session.SendJsonAsync(new { type = "shareId", clientId = session.ClientId }, cancellationToken);
            await BroadcastMaskAsync(room, cancellationToken);

            if (!string.IsNullOrWhiteSpace(join.PlayerName) && join.PlayerId.HasValue)
            {
                await room.BroadcastTextAsync(session.ClientId, JsonSerializer.Serialize(new
                {
                    type = "profile",
                    clientId = session.ClientId,
                    playerName = join.PlayerName,
                    playerId = join.PlayerId.Value,
                }));
            }

            await PumpLoopAsync(room, session, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException ex)
        {
            _logger.LogDebug(ex, "Voice websocket closed with exception");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled voice websocket error");
        }
        finally
        {
            if (room != null && session != null)
            {
                room.Leave(session.ClientId);
                await BroadcastMaskAsync(room, CancellationToken.None);
                await room.BroadcastTextAsync(session.ClientId, JsonSerializer.Serialize(new { type = "clientLeft", clientId = session.ClientId }));
                roomManager.RemoveRoomIfEmpty(room);
            }

            if (session != null)
            {
                await session.CloseAsync("bye", CancellationToken.None);
            }
        }
    }

    private async Task PumpLoopAsync(VoiceRoom room, VoiceSession session, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            while (!cancellationToken.IsCancellationRequested && !session.IsClosed)
            {
                var result = await sessionReceiveAsync(session, buffer, cancellationToken);
                if (result.CloseRequested)
                {
                    break;
                }

                if (result.Count == 0)
                {
                    continue;
                }

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    var packet = new byte[result.Count];
                    Buffer.BlockCopy(buffer, 0, packet, 0, result.Count);
                    await room.BroadcastBinaryAsync(session.ClientId, packet);
                    continue;
                }

                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await HandleControlTextAsync(room, session, text, cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task<(int Count, WebSocketMessageType MessageType, bool CloseRequested)> sessionReceiveAsync(
        VoiceSession session,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        int total = 0;
        WebSocketMessageType type = WebSocketMessageType.Text;

        while (true)
        {
            var segment = new ArraySegment<byte>(buffer, total, buffer.Length - total);
            var result = await sessionReceiveRawAsync(session, segment, cancellationToken);
            if (result.CloseRequested)
            {
                return (0, result.MessageType, true);
            }

            type = result.MessageType;
            total += result.Count;
            if (result.EndOfMessage)
            {
                return (total, type, false);
            }
        }
    }

    private static async Task<(int Count, bool EndOfMessage, WebSocketMessageType MessageType, bool CloseRequested)> sessionReceiveRawAsync(
        VoiceSession session,
        ArraySegment<byte> segment,
        CancellationToken cancellationToken)
    {
        var result = await session.ReceiveAsync(segment, cancellationToken);
        return (result.Count, result.EndOfMessage, result.MessageType, result.MessageType == WebSocketMessageType.Close);
    }

    private static async Task<JoinPayload?> ReceiveJoinAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var result = await socket.ReceiveAsync(buffer, cancellationToken);
        if (result.MessageType != WebSocketMessageType.Text || result.Count <= 0)
        {
            return null;
        }

        var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("type", out var typeProp) || typeProp.GetString() != "join")
            {
                return null;
            }

            var region = doc.RootElement.TryGetProperty("region", out var r) ? r.GetString() ?? "default" : "default";
            var roomCode = doc.RootElement.TryGetProperty("roomCode", out var code) ? code.GetString() ?? "default" : "default";
            var playerName = doc.RootElement.TryGetProperty("playerName", out var name) ? name.GetString() : null;
            byte? playerId = null;
            if (doc.RootElement.TryGetProperty("playerId", out var pid) && pid.TryGetByte(out var b))
            {
                playerId = b;
            }

            return new JoinPayload(region, roomCode, playerName, playerId);
        }
        catch
        {
            return null;
        }
    }

    private static async Task BroadcastMaskAsync(VoiceRoom room, CancellationToken cancellationToken)
    {
        var maskJson = JsonSerializer.Serialize(new { type = "tracks", mask = room.CurrentMask });
        foreach (var session in room.Sessions)
        {
            await session.SendTextAsync(maskJson, cancellationToken);
        }
    }

    private static async Task HandleControlTextAsync(VoiceRoom room, VoiceSession session, string text, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(text);
        if (!doc.RootElement.TryGetProperty("type", out var typeNode))
        {
            return;
        }

        var type = typeNode.GetString();
        switch (type)
        {
            case "profile":
            {
                var playerName = doc.RootElement.TryGetProperty("playerName", out var name) ? name.GetString() : null;
                byte? playerId = null;
                if (doc.RootElement.TryGetProperty("playerId", out var pid) && pid.TryGetByte(out var b))
                {
                    playerId = b;
                }

                session.UpdateProfile(playerName, playerId);
                await room.BroadcastTextAsync(session.ClientId, JsonSerializer.Serialize(new
                {
                    type = "profile",
                    clientId = session.ClientId,
                    playerName,
                    playerId,
                }));
                break;
            }
            case "mute":
            {
                var mute = doc.RootElement.TryGetProperty("mute", out var muteNode) && muteNode.GetBoolean();
                session.UpdateMute(mute);
                await room.BroadcastTextAsync(session.ClientId, JsonSerializer.Serialize(new
                {
                    type = "mute",
                    clientId = session.ClientId,
                    mute,
                }));
                break;
            }
            case "reload":
            {
                await session.SendTextAsync(JsonSerializer.Serialize(new { type = "tracks", mask = room.CurrentMask }), cancellationToken);
                break;
            }
        }
    }

    private sealed record JoinPayload(string Region, string RoomCode, string? PlayerName, byte? PlayerId);
}
