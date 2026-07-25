// Every Role a non-killer player can hold. This is the hidden,
// server-authoritative Role (see NetworkPlayerEntity.Role) — distinct from
// the visible Mask (NetworkPlayerEntity.CurrentMaskId), which is also a
// PlayerRole value but can diverge from Role for the killer.
public enum PlayerRole
{
    Visitor,
    Detective,
    Security,
    Stalker,
    Surveillance,
    Bookkeeper,
    Neighbor
}