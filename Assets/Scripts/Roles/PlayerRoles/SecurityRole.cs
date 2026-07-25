using System.Collections.Generic;
using UnityEngine;

// Each round, auto-protects one random living player (can't be the
// killer). A blocked kill does NOT consume the killer's round attempt —
// that half of the rule lives in NetworkPlayerAttack's kill resolution,
// not here; this role's only job is picking who's protected.
public class SecurityRole : PlayerRoleBehaviour
{
    public override PlayerRole RoleId => PlayerRole.Security;

    public override void Subscribe(NetworkPlayerEntity owner)
    {
        base.Subscribe(owner);
        GameEventManager.RoundStarted += HandleRoundStarted;
    }

    public override void Unsubscribe()
    {
        GameEventManager.RoundStarted -= HandleRoundStarted;
    }

    private void HandleRoundStarted(int roundNumber)
    {
        RoleAbility(Owner);
    }

    // Relies on RoamingPhase.OnEnter having already called
    // NetworkPlayerEntity.ResetRoundState() on everyone (clearing last
    // round's IsProtected) BEFORE raising RoundStarted — see the ordering
    // note there.
    public override void RoleAbility(NetworkPlayerEntity self)
    {
        List<NetworkPlayerController> candidates = new List<NetworkPlayerController>();
        foreach (NetworkPlayerController player in PhaseContext.GetAllPlayersStatic())
        {
            if (!player.IsKillerRole && player.IsAlive)
            {
                candidates.Add(player);
            }
        }

        if (candidates.Count == 0) return;

        NetworkPlayerController protectedPlayer = candidates[Random.Range(0, candidates.Count)];
        protectedPlayer.Entity.SetProtected(true);

        Debug.Log($"[SecurityRole] Protecting client {protectedPlayer.OwnerClientId} this round.");
        self.PushRoleEventRpc($"You protected {PlayerDisplayName.ForColored(protectedPlayer)} this round.");
    }
}
