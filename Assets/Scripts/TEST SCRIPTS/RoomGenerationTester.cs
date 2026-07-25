using UnityEngine;

// TEMPORARY, TEST-ONLY. Not part of the actual game — delete this once
// generation is confirmed working and NetworkPlayerController exists to
// test real player movement instead.
//
// Implements IRoomOccupant directly so it can walk a fake occupant through
// RoomsManager's movement resolution without needing colliders, a real
// player, or Netcode running at all.
public class RoomGenerationTester : MonoBehaviour, IRoomOccupant
{
    [Header("Test Controls")]
    [SerializeField] private KeyCode regenerateMappingKey = KeyCode.R;
    [SerializeField] private KeyCode moveNorthKey = KeyCode.UpArrow;
    [SerializeField] private KeyCode moveSouthKey = KeyCode.DownArrow;
    [SerializeField] private KeyCode moveEastKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode moveWestKey = KeyCode.LeftArrow;

    [Header("Test As")]
    [Tooltip("Toggle to test the killer's real-adjacency path instead of the shared randomized mapping.")]
    [SerializeField] private bool testAsKiller = false;

    private RoomObject _currentRoom;

    // --- IRoomOccupant ---
    ulong IRoomOccupant.ClientId => 9999;
    bool IRoomOccupant.IsKillerRole => testAsKiller;
    bool IRoomOccupant.IsOwner => true;

    void IRoomOccupant.RequestMoveThroughDoor(RoomObject fromRoom, RoomsManager.Direction direction)
    {
        // Not used — this tester calls RoomsManager.RequestMoveThroughExit
        // directly from TryMove() below instead of going through a trigger.
    }

    void IRoomOccupant.NotifyRoomChanged(int newRoomIndex, RoomObject newRoom)
    {
        _currentRoom = newRoom;
        Debug.Log($"[RoomGenerationTester] Moved into room {newRoomIndex} at ({newRoom.Row},{newRoom.Col}).");
    }

    private void Start()
    {
        if (RoomsManager.Instance == null)
        {
            Debug.LogError("[RoomGenerationTester] No RoomsManager found in the scene.");
            enabled = false;
            return;
        }

        LogAllRooms();

        RoomsManager.Instance.GenerateExitMapping();
        Debug.Log("[RoomGenerationTester] Exit mapping generated.");

        _currentRoom = RoomsManager.Instance.GetRoomByIndex(0);
        if (_currentRoom == null)
        {
            Debug.LogError("[RoomGenerationTester] Room 0 doesn't exist — generation likely failed.");
            enabled = false;
            return;
        }

        Debug.Log("[RoomGenerationTester] Starting at room 0. Arrow keys to move, R to reroll the mapping.");
    }

    private void Update()
    {
        if (Input.GetKeyDown(regenerateMappingKey))
        {
            RoomsManager.Instance.GenerateExitMapping();
            Debug.Log("[RoomGenerationTester] Exit mapping regenerated.");
        }

        TryMove(moveNorthKey, RoomsManager.Direction.North);
        TryMove(moveSouthKey, RoomsManager.Direction.South);
        TryMove(moveEastKey, RoomsManager.Direction.East);
        TryMove(moveWestKey, RoomsManager.Direction.West);
    }

    private void TryMove(KeyCode key, RoomsManager.Direction direction)
    {
        if (!Input.GetKeyDown(key)) return;

        bool moved = RoomsManager.Instance.RequestMoveThroughExit(_currentRoom, direction, this);
        if (!moved)
        {
            Debug.Log($"[RoomGenerationTester] Move {direction} failed — no door that way, " +
                      $"or the exit mapping hasn't been generated yet.");
        }
    }

    // Logs every room's coordinates and real exits, so you can eyeball that
    // corners have 2, edges have 3, and interior rooms have all 4.
    private void LogAllRooms()
    {
        int roomCount = 0;

        for (int i = 0; i < 1000; i++) // generous upper bound, stops at first null
        {
            RoomObject room = RoomsManager.Instance.GetRoomByIndex(i);
            if (room == null) break;

            roomCount++;
            string exits = "";
            foreach (RoomsManager.Direction dir in System.Enum.GetValues(typeof(RoomsManager.Direction)))
            {
                if (room.HasExit(dir)) exits += dir + " ";
            }

            Debug.Log($"[RoomGenerationTester] Room {i} @ ({room.Row},{room.Col}) — exits: {exits}");
        }

        Debug.Log($"[RoomGenerationTester] Total rooms found: {roomCount} (expected 25 for a 5x5 grid).");
    }
}