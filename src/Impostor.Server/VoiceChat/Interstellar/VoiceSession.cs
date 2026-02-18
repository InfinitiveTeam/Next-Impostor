using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.VoiceChat.Interstellar;

internal sealed class VoiceSession
{
    private readonly WebSocket _socket;
    private readonly ILogger _logger;

    public VoiceSession(byte clientId, WebSocket socket, ILogger logger)
    {
        ClientId = clientId;
        _socket = socket;
        _logger = logger;
    }

    public byte ClientId { get; }

    public bool IsClosed => _socket.State is WebSocketState.Closed or WebSocketState.Aborted;

    public string? PlayerName { get; private set; }

    public byte? PlayerId { get; private set; }

    public bool IsMute { get; private set; }

    public void UpdateProfile(string? playerName, byte? playerId)
    {
        PlayerName = playerName;
        PlayerId = playerId;
    }

    public void UpdateMute(bool mute)
    {
        IsMute = mute;
    }

    public async Task SendTextAsync(string message, CancellationToken cancellationToken = default)
    {
        if (IsClosed)
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(message);
        await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    public async Task SendJsonAsync<T>(T payload, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(payload);
        await SendTextAsync(json, cancellationToken);
    }

    public async Task SendBinaryAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (IsClosed)
        {
            return;
        }

        await _socket.SendAsync(data, WebSocketMessageType.Binary, true, cancellationToken);
    }

    public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken = default)
    {
        return _socket.ReceiveAsync(buffer, cancellationToken);
    }

    public async Task CloseAsync(string reason, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_socket.State == WebSocketState.Open)
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Voice session {ClientId} close ignored", ClientId);
        }
    }
}
