// Owns "the killer wears their last victim's mask" — the one bit of mask
// logic guaranteed to exist regardless of which Tier 2/3 killer roles
// (Forger, Impostor) eventually get built on top of it. Static and
// stateless: nothing here needs to persist between calls. Separated out
// from the kill action itself so those future killer roles have an
// obvious, single seam to override instead of editing kill resolution.
public static class MaskManager
{
    // Server-only. Call the moment a kill resolves.
    public static void ReassignKillerMask(NetworkPlayerEntity killerEntity, PlayerRole victimRole)
    {
        killerEntity.SetMaskToVictimRole(victimRole);
    }
}