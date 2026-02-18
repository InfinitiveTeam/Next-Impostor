using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Impostor.Server.VoiceChat.Interstellar;

internal sealed class VoiceRoom
{
    private readonly ConcurrentDictionary<byte, VoiceSession> _sessions = new();

    public VoiceRoom(string key)
    {
        Key = key;
    }

    public string Key { get; }

    public int Count => _sessions.Count;

    public IEnumerable<VoiceSession> Sessions => _sessions.Values;

    public byte NextClientId()
    {
        byte id = 0;
        while (_sessions.ContainsKey(id))
        {
            id++;
        }

        return id;
    }

    public void Join(VoiceSession session)
    {
        _sessions[session.ClientId] = session;
    }

    public bool Leave(byte clientId)
    {
        return _sessions.TryRemove(clientId, out _);
    }

    public long CurrentMask => _sessions.Keys.Aggregate(0L, (mask, id) => mask | (1L << id));

    public Task BroadcastTextAsync(byte senderId, string payload)
    {
        return Task.WhenAll(_sessions.Values
            .Where(s => s.ClientId != senderId)
            .Select(s => s.SendTextAsync(payload)));
    }

    public Task BroadcastBinaryAsync(byte senderId, byte[] payload)
    {
        return Task.WhenAll(_sessions.Values
            .Where(s => s.ClientId != senderId)
            .Select(s => s.SendBinaryAsync(payload)));
    }
}
