// Notified when and where a player is killed (time + room), regardless of
// who the victim was. Entirely event-driven — no polling, no per-tick work.
public class DetectiveRole : PlayerRoleBehaviour
{
    public override PlayerRole RoleId => PlayerRole.Detective;

    // Stashed by HandlePlayerKilled so RoleAbility(self) — which only takes
    // self, per the base class contract — has something to act on.
    private int _lastKillRoomIndex;

    public override void Subscribe(NetworkPlayerEntity owner)
    {
        base.Subscribe(owner);
        GameEventManager.PlayerKilled += HandlePlayerKilled;
    }

    public override void Unsubscribe()
    {
        GameEventManager.PlayerKilled -= HandlePlayerKilled;
    }

    private void HandlePlayerKilled(ulong victimClientId, ulong killerClientId, int roomIndex)
    {
        _lastKillRoomIndex = roomIndex;
        RoleAbility(Owner);
    }

    // Delivers to the Role Event Panel on the Detective's own client — see
    // NetworkPlayerEntity.PushRoleEventRpc, the shared delivery method every
    // info-role uses instead of a bespoke RPC/UI per role.
    public override void RoleAbility(NetworkPlayerEntity self)
    {
        self.PushRoleEventRpc($"A body was found in Room {_lastKillRoomIndex}.");
    }
}
