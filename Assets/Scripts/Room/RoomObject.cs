using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// One physical room in the manor. Holds this room's real doors, tracks who's
// currently standing in it, and knows its own grid identity — all the "per
// room" bookkeeping the design doc originally split across a central
// occupancy dictionary now lives here instead, one list per room.
//
// A door's Direction is declared exactly once, on its RoomExitTrigger child —
// there's no separately-authored exit list to keep in sync with it. Which
// doors are currently ACTIVE (real, given this room's grid position) is
// decided by RoomsManager.ConfigureRealExits() right after instantiation.
public class RoomObject : MonoBehaviour
{
    [Header("Identity (assigned by RoomsManager at startup — don't set manually)")]
    public int RoomIndex { get; private set; } = -1;
    public int Row { get; private set; } = -1;
    public int Col { get; private set; } = -1;

    private readonly Dictionary<RoomsManager.Direction, RoomExitTrigger> _activeExits =
        new Dictionary<RoomsManager.Direction, RoomExitTrigger>();

    public List<ulong> Occupants { get; } = new List<ulong>();

    [Header("Entry Point")]
    [Tooltip("Where an incoming player is placed after being routed into this " +
             "room — needed because the randomized mapping usually sends them " +
             "somewhere NOT physically adjacent in the scene, so it's a " +
             "teleport, not a walk. Defaults to this room's own transform if left empty.")]
    [SerializeField] private Transform entryPoint;
    public Transform EntryPoint => entryPoint != null ? entryPoint : transform;

    // Called once by RoomsManager during AssignCoordinates(). Not meant to
    // be called from anywhere else.
    public void AssignCoordinates(int index, int row, int col)
    {
        RoomIndex = index;
        Row = row;
        Col = col;
    }

    public void ConfigureRealExits(IReadOnlyCollection<RoomsManager.Direction> realDirections)
    {
        foreach (RoomExitTrigger trigger in GetComponentsInChildren<RoomExitTrigger>(true))
        {
            trigger.gameObject.SetActive(realDirections.Contains(trigger.Direction));
        }
        RefreshActiveExits();
    }

    public void RefreshActiveExits()
    {
        _activeExits.Clear();
        foreach (RoomExitTrigger trigger in GetComponentsInChildren<RoomExitTrigger>(false))
        {
            _activeExits[trigger.Direction] = trigger;
        }
    }

    public bool HasExit(RoomsManager.Direction direction) => _activeExits.ContainsKey(direction);

    public void AddOccupant(ulong clientId)
    {
        if (!Occupants.Contains(clientId))
        {
            Occupants.Add(clientId);
        }
    }

    public void RemoveOccupant(ulong clientId)
    {
        Occupants.Remove(clientId);
    }
}