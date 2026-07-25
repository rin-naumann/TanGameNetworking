public interface IRoomOccupant
{
    ulong ClientId { get; }
    bool IsKillerRole { get; }
    bool IsOwner { get; }
    void RequestMoveThroughDoor(RoomObject fromRoom, RoomsManager.Direction direction);
    void NotifyRoomChanged(int newRoomIndex, RoomObject newRoom);
}