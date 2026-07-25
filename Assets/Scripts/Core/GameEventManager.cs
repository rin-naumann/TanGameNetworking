using System;

public static class GameEventManager
{
    public static event Action<ulong, int, int> PlayerRelocated;
    public static event Action<ulong, ulong, int> PlayerKilled;
    public static event Action<int> RoundStarted;
    public static event Action<ulong, int> PlayerVotedOut;

    private static int _roundNumber;

    public static void RaisePlayerRelocated(ulong clientId, int fromRoomIndex, int toRoomIndex) =>
        PlayerRelocated?.Invoke(clientId, fromRoomIndex, toRoomIndex);

    public static void RaisePlayerKilled(ulong victimClientId, ulong killerClientId, int roomIndex) =>
        PlayerKilled?.Invoke(victimClientId, killerClientId, roomIndex);

    public static void RaisePlayerVotedOut(ulong victimClientId) =>
        PlayerVotedOut?.Invoke(victimClientId, -1);

    public static void RaiseRoundStarted()
    {
        _roundNumber++;
        RoundStarted?.Invoke(_roundNumber);
    }

    public static void ResetForNewMatch()
    {
        _roundNumber = 0;
        PlayerRelocated = null;
        // PlayerKilled/PlayerVotedOut stay subscribed across matches since they're wired up once at connection time, before this ever runs.
        RoundStarted = null;
    }
}