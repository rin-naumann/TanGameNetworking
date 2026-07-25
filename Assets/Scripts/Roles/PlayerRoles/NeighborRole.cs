using UnityEngine;

// Notified when the killer is in a room orthogonally adjacent to this
// player's current room. Recomputed on every PlayerRelocated event, since
// either side of the "adjacent" relationship (self moving or killer
// moving) can be what changed it. Uses real grid adjacency (Row/Col), not
// the shuffled per-round exit mapping non-killer players actually walk
// through - matching how the killer's own movement already resolves via
// RoomsManager's real-neighbor logic.
//
// Debounced on the transition into adjacency (_wasAdjacent) rather than
// firing every single relocation event while already adjacent, so standing
// next to the killer for several moves in a row doesn't spam the panel.
public class NeighborRole : PlayerRoleBehaviour
{
    public override PlayerRole RoleId => PlayerRole.Neighbor;

    private bool _wasAdjacent;

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
        RoleAbility(Owner);
    }

    public override void RoleAbility(NetworkPlayerEntity self)
    {
        if (!self.IsAlive) return;

        NetworkPlayerController killer = null;
        foreach (NetworkPlayerController player in PhaseContext.GetAllPlayersStatic())
        {
            if (player.IsKillerRole)
            {
                killer = player;
                break;
            }
        }
        if (killer == null) return;

        RoomObject selfRoom = RoomsManager.Instance.GetRoomByIndex(self.CurrentRoomIndex);
        RoomObject killerRoom = RoomsManager.Instance.GetRoomByIndex(killer.CurrentRoomIndex);
        if (selfRoom == null || killerRoom == null) return;

        bool isAdjacent = IsAdjacent(selfRoom, killerRoom);

        if (isAdjacent && !_wasAdjacent)
        {
            self.PushRoleEventRpc("The killer is in a neighboring room.");
        }

        _wasAdjacent = isAdjacent;
    }

    private static bool IsAdjacent(RoomObject a, RoomObject b)
    {
        int rowDelta = Mathf.Abs(a.Row - b.Row);
        int colDelta = Mathf.Abs(a.Col - b.Col);
        return (rowDelta == 1 && colDelta == 0) || (rowDelta == 0 && colDelta == 1);
    }
}
