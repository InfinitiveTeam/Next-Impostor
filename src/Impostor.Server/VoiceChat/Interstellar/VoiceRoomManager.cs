using System.Collections.Concurrent;

namespace Impostor.Server.VoiceChat.Interstellar;

internal sealed class VoiceRoomManager
{
    private readonly ConcurrentDictionary<string, VoiceRoom> _rooms = new();

    public VoiceRoom GetRoom(string region, string roomCode)
    {
        var key = $"{region}.{roomCode}";
        return _rooms.GetOrAdd(key, static roomKey => new VoiceRoom(roomKey));
    }

    public void RemoveRoomIfEmpty(VoiceRoom room)
    {
        if (room.Count == 0)
        {
            _rooms.TryRemove(room.Key, out _);
        }
    }
}
