using System.Collections.Generic;

// Reports every non-killer room entry (room only -- deliberately does NOT
// reveal who entered), oldest-to-newest, capped at the last 5.
// PushRoleEventRpc only carries a single string, so rather than build a
// separate multi-line delivery path, the whole capped feed is re-rendered
// as one block and resent on every new entry.
public class BookkeeperRole : PlayerRoleBehaviour
{
    public override PlayerRole RoleId => PlayerRole.Bookkeeper;

    private const int MaxEntries = 5;
    private readonly Queue<string> _entries = new Queue<string>();

    public override void Subscribe(NetworkPlayerEntity owner)
    {
        base.Subscribe(owner);
        GameEventManager.PlayerRelocated += HandlePlayerRelocated;
    }

    public override void Unsubscribe()
    {
        GameEventManager.PlayerRelocated -= HandlePlayerRelocated;
    }

    private void HandlePlayerRelocated(ulong clientId, int fromRoomIndex, int toRoomIndex)
    {
        // clientId is only used here to resolve IsKillerRole for filtering --
        // it is never included in the entry text itself, so the feed never
        // reveals who moved, only that someone did.
        NetworkPlayerController mover = FindByClientId(clientId);
        if (mover == null) return;
        if (mover.IsKillerRole) return; // Bookkeeper does not track the killer.

        _entries.Enqueue($"Someone entered Room {toRoomIndex}.");
        while (_entries.Count > MaxEntries)
        {
            _entries.Dequeue();
        }

        RoleAbility(Owner);
    }

    private static NetworkPlayerController FindByClientId(ulong clientId)
    {
        foreach (NetworkPlayerController player in PhaseContext.GetAllPlayersStatic())
        {
            if (player.ClientId == clientId) return player;
        }
        return null;
    }

    public override void RoleAbility(NetworkPlayerEntity self)
    {
        self.PushRoleEventRpc(string.Join("\n", _entries));
    }
}
