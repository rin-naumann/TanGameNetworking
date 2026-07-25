using System.Collections.Generic;
using UnityEngine;

// Reassigned every round: locks onto one random OTHER living player and
// tracks them persistently for the rest of the round. Fires immediately on
// assignment, then again every time that specific target moves rooms
// (via GameEventManager.PlayerRelocated) until the next round reassigns.
public class StalkerRole : PlayerRoleBehaviour
{
    public override PlayerRole RoleId => PlayerRole.Stalker;

    // The client we're currently locked onto for this round. Null when no
    // living candidate was available at assignment time.
    private ulong? _targetClientId;

    public override void Subscribe(NetworkPlayerEntity owner)
    {
        base.Subscribe(owner);
        GameEventManager.RoundStarted += HandleRoundStarted;
        GameEventManager.PlayerRelocated += HandlePlayerRelocated;
    }

    public override void Unsubscribe()
    {
        GameEventManager.RoundStarted -= HandleRoundStarted;
        GameEventManager.PlayerRelocated -= HandlePlayerRelocated;
    }

    private void HandleRoundStarted(int roundNumber)
    {
        RoleAbility(Owner);
    }

    // Picks fresh every round (never cached across rounds) so a target who
    // moves after being picked still reads their room at push-time, not
    // stale data. The pick IS cached for the duration of the round so
    // subsequent moves by this same target keep reporting through
    // HandlePlayerRelocated instead of going silent after one push.
    public override void RoleAbility(NetworkPlayerEntity self)
    {
        List<NetworkPlayerController> candidates = new List<NetworkPlayerController>();
        foreach (NetworkPlayerController player in PhaseContext.GetAllPlayersStatic())
        {
            if (player.Entity == self) continue; // never target self
            if (player.IsAlive)
            {
                candidates.Add(player);
            }
        }

        if (candidates.Count == 0)
        {
            _targetClientId = null;
            return;
        }

        NetworkPlayerController target = candidates[Random.Range(0, candidates.Count)];
        _targetClientId = target.ClientId;
        self.PushRoleEventRpc($"You sense {PlayerDisplayName.ForColored(target)} is in Room {target.CurrentRoomIndex}.");
    }

    // Fires on EVERY player relocation; only report when the mover is our
    // locked-on target for this round, so tracking persists across however
    // many room changes they make rather than expiring after one push.
    private void HandlePlayerRelocated(ulong clientId, int fromRoomIndex, int toRoomIndex)
    {
        if (_targetClientId == null || clientId != _targetClientId.Value) return;
        Owner.PushRoleEventRpc($"You sense {PlayerDisplayName.ForColored(clientId)} moved to Room {toRoomIndex}.");
    }
}
