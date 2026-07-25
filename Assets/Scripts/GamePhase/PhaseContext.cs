using System.Collections.Generic;
using Unity.Netcode;

// Small bundle of everything an IGamePhase implementation might need to
// touch, handed in on every Enter/Tick/Exit/GetNextPhase call rather than
// each phase reaching for FindFirstObjectByType or NetworkManager.Singleton
// itself. Lives entirely server-side — phases never run client-side.
public class PhaseContext
{
    public PhaseManager Manager { get; }
    public RoomsManager Rooms => RoomsManager.Instance;

    public PhaseContext(PhaseManager manager)
    {
        Manager = manager;
    }

    // Walks connected clients fresh every call rather than caching a
    // snapshot — the roster (and who's still alive) changes across a match,
    // and a stale list here would be a subtle bug waiting to happen later.
    public IEnumerable<NetworkPlayerController> GetAllPlayers() => GetAllPlayersStatic();

    // Static so non-phase code (kill resolution, which needs to find the
    // Detective and count survivors) can reuse this without needing a
    // PhaseContext instance.
    public static IEnumerable<NetworkPlayerController> GetAllPlayersStatic()
    {
        if (NetworkManager.Singleton == null) yield break;

        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;

            NetworkPlayerController controller = client.PlayerObject.GetComponent<NetworkPlayerController>();
            if (controller != null) yield return controller;
        }
    }

    // Moved here from PhaseManager (was a private static method on it) so
    // VotingPhase.GetNextPhase can resolve a clientId to a controller
    // without needing anything PhaseManager doesn't already expose through
    // the context.
    public NetworkPlayerController FindPlayerByClientId(ulong clientId)
    {
        foreach (NetworkPlayerController player in GetAllPlayersStatic())
        {
            if (player.ClientId == clientId) return player;
        }
        return null;
    }
}