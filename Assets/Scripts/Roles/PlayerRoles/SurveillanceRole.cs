using UnityEngine;

// Notified of the killer's room any time the killer shares a room with ANY
// living player -- not just Surveillance's own location, unlike Neighbor
// which only cares about ITS OWN player's adjacency. Recomputed on every
// PlayerRelocated event, since either side (killer moving into an occupied
// room, or a player walking into the killer's room) can trigger it.
//
// Debounced on the transition into "killer is co-located with someone"
// (_wasTriggered) rather than firing every relocation while already true,
// matching NeighborRole's debounce so standing in the killer's room for
// several moves in a row doesn't spam the panel.
public class SurveillanceRole : PlayerRoleBehaviour
{
    public override PlayerRole RoleId => PlayerRole.Surveillance;

    private bool _wasTriggered;

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

        RoomObject killerRoom = RoomsManager.Instance.GetRoomByIndex(killer.CurrentRoomIndex);
        if (killerRoom == null) return;

        // Killer shares the room with someone living if the room's occupant
        // list has more than just the killer themself in it. Uses the room's
        // own occupant list rather than re-deriving it from every player's
        // CurrentRoomIndex, since that list is already the authoritative,
        // continuously-maintained source (RoomsManager.ExecuteMove keeps it
        // in sync on every move).
        bool isTriggered = killerRoom.Occupants.Count > 1;

        if (isTriggered && !_wasTriggered)
        {
            self.PushRoleEventRpc($"The killer is in Room {killerRoom.RoomIndex} with someone.");
        }

        _wasTriggered = isTriggered;
    }
}
