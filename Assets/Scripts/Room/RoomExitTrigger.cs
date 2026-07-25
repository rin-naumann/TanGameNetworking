using UnityEngine;

public class RoomExitTrigger : MonoBehaviour
{
    [Tooltip("Which way this door faces. Set once here — nothing else stores " +
             "this separately, so there's nothing to fall out of sync.")]
    [SerializeField] private RoomsManager.Direction direction;
    public RoomsManager.Direction Direction => direction;

    // Found automatically rather than dragged in manually, since prefab
    // instances can't have a serialized reference to "their own root"
    // baked into the prefab asset ahead of time.
    private RoomObject _parentRoom;

    private void Awake()
    {
        _parentRoom = GetComponentInParent<RoomObject>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Looks for IRoomOccupant on the thing that walked in — implemented
        // by the player's networked controller once that's built (Day 1,
        // step 3). Anything without that component (props, decorations,
        // stray colliders) is silently ignored.
        IRoomOccupant occupant = other.GetComponentInParent<IRoomOccupant>();
        if (occupant == null) return;

        // Only act for the player this machine actually owns. Physics
        // detection runs locally on every machine simulating this collider,
        // so without this check every client would also "detect" every
        // OTHER player's movement and try (and fail) to act on it.
        if (!occupant.IsOwner) return;

        if (_parentRoom == null)
        {
            Debug.LogWarning($"[RoomExitTrigger] {name} has no RoomObject in its parent hierarchy.");
            return;
        }

        // Not calling RoomsManager directly here on purpose — this may be
        // running on a non-host client, where RoomsManager's server-only
        // guard would just silently reject it. The occupant's own
        // implementation is responsible for getting this request to the
        // server (via RPC, using this room's RoomIndex — never the
        // RoomObject reference itself, which can't cross the network)
        // before RoomsManager ever sees it.
        occupant.RequestMoveThroughDoor(_parentRoom, direction);
    }
}