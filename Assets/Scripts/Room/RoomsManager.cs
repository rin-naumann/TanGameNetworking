using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Coordinates the manor's rooms: assigns them grid coordinates, owns the
// shared randomized exit mapping (regenerated once per round), and resolves
// both movement paths (killer's real adjacency, players' shared shuffled
// mapping). Individual room state (exits, occupants) lives on RoomObject —
// this script only coordinates between rooms, it doesn't hold per-room data.
public class RoomsManager : MonoBehaviour
{
    public static RoomsManager Instance { get; private set; }

    public enum Direction { North, South, East, West }
    private static readonly Direction[] AllDirections =
        { Direction.North, Direction.South, Direction.East, Direction.West };

    [Header("Grid Size")]
    [Tooltip("Bounds the row/col assignment for-loop below.")]
    [SerializeField] private int width = 5;
    [SerializeField] private int height = 5;

    [Header("Prefab Generation")]
    [Tooltip("If assigned, RoomsManager instantiates width*height copies of this " +
             "in Awake instead of expecting a manually-placed Rooms list.")]
    [SerializeField] private RoomObject roomPrefab;
    [SerializeField] private Transform roomsParent;
    [Tooltip("World-space distance between room origins.")]
    [SerializeField] private Vector3 roomSpacing = new Vector3(20f, 0f, 20f);

    [Header("Rooms")]
    [Tooltip("Auto-filled if Room Prefab is assigned above. Otherwise, drag every " +
             "RoomObject in manually, in row-major order: all of row 0 left-to-right, " +
             "then row 1, etc. Order here IS the layout.")]
    [SerializeField] private List<RoomObject> rooms = new List<RoomObject>();

    // Fast lookups, built once coordinates are assigned.
    private readonly Dictionary<int, RoomObject> _roomByIndex = new Dictionary<int, RoomObject>();
    private readonly Dictionary<(int row, int col), RoomObject> _roomByCoordinate = new Dictionary<(int, int), RoomObject>();

    // The shared, server-only "where does this door actually lead" table.
    // Regenerated once per round by GenerateExitMapping(). Same mapping for
    // every player — that's what makes rounds disorienting but mappable
    // within the round, per the design decision.
    private readonly Dictionary<(RoomObject room, Direction dir), RoomObject> _exitMapping =
        new Dictionary<(RoomObject, Direction), RoomObject>();

    private readonly System.Random _rng = new System.Random();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (roomPrefab != null)
        {
            GenerateGridFromPrefab();
        }
        else
        {
            AssignCoordinates();
        }
    }

    // --- Setup: prefab-driven generation ---

    // Instantiates width*height copies of roomPrefab, positions them by grid
    // coordinate, then figures out which of each room's doors are real
    // (based purely on grid bounds) and disables the rest. Runs identically
    // on every machine — same prefab, same width/height/spacing, no
    // randomness — so no networking is needed here; every client ends up
    // with the same 25 rooms at the same indices independently.
    private void GenerateGridFromPrefab()
    {
        rooms.Clear();

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                Vector3 position = new Vector3(col * roomSpacing.x, 0f, row * roomSpacing.z);
                RoomObject room = Instantiate(roomPrefab, position, Quaternion.identity, roomsParent);
                rooms.Add(room);
            }
        }

        AssignCoordinates();
        ConfigureAllRealExits();
    }

    // For every generated room, works out which directions actually lead to
    // an in-bounds neighbor and tells the room to disable the rest of its
    // prefab's pre-wired doors. Corner rooms end up with 2 active doors,
    // edges with 3, interior rooms with all 4.
    private void ConfigureAllRealExits()
    {
        foreach (RoomObject room in rooms)
        {
            var realDirections = new HashSet<Direction>();

            if (GetRoomByCoordinate(room.Row - 1, room.Col) != null) realDirections.Add(Direction.North);
            if (GetRoomByCoordinate(room.Row + 1, room.Col) != null) realDirections.Add(Direction.South);
            if (GetRoomByCoordinate(room.Row, room.Col + 1) != null) realDirections.Add(Direction.East);
            if (GetRoomByCoordinate(room.Row, room.Col - 1) != null) realDirections.Add(Direction.West);

            room.ConfigureRealExits(realDirections);
        }
    }

    // --- Setup: manual placement (fallback if no prefab is assigned) ---

    // Walks the developer-ordered Rooms list and hands each RoomObject its
    // index/row/col, bounded by width/height. This is the only place grid
    // coordinates get decided — everything else just reads them.
    private void AssignCoordinates()
    {
        if (rooms.Count != width * height)
        {
            Debug.LogError($"[RoomsManager] Expected {width * height} rooms ({width}x{height}) " +
                            $"but the Rooms list has {rooms.Count}. Fix this before playing.");
        }

        int index = 0;
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                if (index >= rooms.Count) return;

                RoomObject room = rooms[index];
                room.AssignCoordinates(index, row, col);

                _roomByIndex[index] = room;
                _roomByCoordinate[(row, col)] = room;

                index++;
            }
        }
    }

    // --- Lookups ---

    public RoomObject GetRoomByIndex(int roomIndex) =>
        _roomByIndex.TryGetValue(roomIndex, out var room) ? room : null;

    public RoomObject GetRoomByCoordinate(int row, int col) =>
        _roomByCoordinate.TryGetValue((row, col), out var room) ? room : null;

    // Used by LobbyReadyManager.BeginMatch() to place each player somewhere
    // valid before the Roaming Phase starts. Called once PER PLAYER (not once
    // for the whole match) so every player lands in an independently
    // randomized room rather than all starting in the same spot. Without
    // this, players never get an initial CurrentRoomIndex, so CurrentRoomUI
    // has nothing to show and every room's Occupants list starts empty.
    //
    // Reuses the same server-only _rng as GenerateExitMapping — this is only
    // ever called from LobbyReadyManager.BeginMatch(), which already only
    // runs on the server.
    public RoomObject GetRandomRoom()
    {
        if (rooms.Count == 0) return null;
        return rooms[_rng.Next(0, rooms.Count)];
    }

    // The true grid neighbor in a direction, ignoring randomization entirely.
    // Used for killer movement and as the basis for validating requests.
    private RoomObject GetRealNeighbor(RoomObject room, Direction direction)
    {
        (int row, int col) = (room.Row, room.Col);
        (int targetRow, int targetCol) = direction switch
        {
            Direction.North => (row - 1, col),
            Direction.South => (row + 1, col),
            Direction.East => (row, col + 1),
            Direction.West => (row, col - 1),
            _ => (row, col)
        };
        return GetRoomByCoordinate(targetRow, targetCol);
    }

    private static bool IsServerAuthoritative() =>
        NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;

    // --- Shared randomized exit mapping (players only) ---

    // Server-only. Rerolls where every real door in the manor leads. Call
    // once at the start of each round — not per move, not per player. Same
    // mapping for everyone, matching the "shared, re-rolled per round"
    // design decision.
    //
    // NOTE: nothing calls this automatically yet — the Roaming/Voting phase
    // timer (Day 1, step 5) doesn't exist. Call it manually to test; wire it
    // to the phase system's round-start event once that's built.
    public void GenerateExitMapping()
    {
        if (!IsServerAuthoritative())
        {
            Debug.LogWarning("[RoomsManager] GenerateExitMapping called on a non-server instance — ignored.");
            return;
        }

        _exitMapping.Clear();

        foreach (RoomObject room in rooms)
        {
            foreach (Direction dir in AllDirections)
            {
                if (!room.HasExit(dir)) continue;

                RoomObject destination;
                do
                {
                    destination = rooms[_rng.Next(0, rooms.Count)];
                } while (destination == room); // never map a door back to its own room

                _exitMapping[(room, dir)] = destination;
            }
        }
    }

    // --- Movement resolution ---

    // Single entry point for both killer and player movement through a
    // physical door. RoomExitTrigger (on RoomObject) calls this when
    // something walks into a doorway. Which path gets taken — real adjacency
    // vs. the shared shuffled mapping — depends entirely on occupant.IsKillerRole.
    public bool RequestMoveThroughExit(RoomObject fromRoom, Direction direction, IRoomOccupant occupant)
    {
        if (!IsServerAuthoritative()) return false;
        if (fromRoom == null || occupant == null) return false;
        if (!fromRoom.HasExit(direction)) return false;

        RoomObject destination = occupant.IsKillerRole
            ? GetRealNeighbor(fromRoom, direction)
            : ResolveRandomizedDestination(fromRoom, direction);

        if (destination == null) return false;

        ExecuteMove(fromRoom, destination, occupant);
        return true;
    }

    // Explicit-target variant for the killer, matching the design doc's
    // MoveToRoom(row, col) — useful if killer movement ever needs to be
    // driven by something other than walking into a door trigger.
    public bool TryMoveKillerToRoom(RoomObject fromRoom, int targetRow, int targetCol, IRoomOccupant killer)
    {
        if (!IsServerAuthoritative()) return false;
        if (fromRoom == null || killer == null || !killer.IsKillerRole) return false;

        RoomObject destination = GetRoomByCoordinate(targetRow, targetCol);
        if (destination == null) return false;

        int rowDelta = Mathf.Abs(fromRoom.Row - targetRow);
        int colDelta = Mathf.Abs(fromRoom.Col - targetCol);
        bool isOrthogonalStep = (rowDelta == 1 && colDelta == 0) || (rowDelta == 0 && colDelta == 1);
        if (!isOrthogonalStep) return false;

        ExecuteMove(fromRoom, destination, killer);
        return true;
    }

    private RoomObject ResolveRandomizedDestination(RoomObject fromRoom, Direction direction)
    {
        if (_exitMapping.TryGetValue((fromRoom, direction), out RoomObject destination))
        {
            return destination;
        }
        Debug.LogWarning("[RoomsManager] No exit mapping yet — call GenerateExitMapping() first.");
        return null;
    }

    // The one place occupancy actually changes hands between rooms, and the
    // occupant is told its new room (every player always knows their current
    // room, per the design doc's UI requirement).
    private void ExecuteMove(RoomObject fromRoom, RoomObject toRoom, IRoomOccupant occupant)
    {
        fromRoom.RemoveOccupant(occupant.ClientId);
        toRoom.AddOccupant(occupant.ClientId);
        occupant.NotifyRoomChanged(toRoom.RoomIndex, toRoom);
    }
}
