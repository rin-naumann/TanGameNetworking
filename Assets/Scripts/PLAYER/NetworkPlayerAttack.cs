using UnityEngine;
using Unity.Netcode;

// The killer's kill action. Adapted from the original reference script per
// the triage doc: keeps the OverlapSphere + break-after-first-hit targeting
// logic as-is, but resolves through the role/state system (IsAlive,
// IsProtected, HasKilledThisRound, MaskManager) instead of a
// NetworkPlayerHealth damage call.
public class NetworkPlayerAttack : NetworkBehaviour
{
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private KeyCode attackKey = KeyCode.Space;

    private NetworkPlayerEntity _entity;
    private NetworkPlayerController _controller;

    private void Awake()
    {
        _entity = GetComponent<NetworkPlayerEntity>();
        _controller = GetComponent<NetworkPlayerController>();
    }

    private void Update()
    {
        if (!IsOwner) return;
        // Client-side gate is just an early-out for non-killers — the
        // server re-checks IsKiller itself below regardless.
        if (!_controller.IsKillerRole) return;

        if (Input.GetKeyDown(attackKey))
        {
            RequestKillRpc();
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestKillRpc()
    {
        if (!_entity.IsKiller || _entity.HasKilledThisRound || !_entity.IsAlive) return;

        Vector3 attackCenter = transform.position + transform.forward;
        Collider[] hits = Physics.OverlapSphere(attackCenter, attackRange, playerLayer);

        foreach (Collider hit in hits)
        {
            // GetComponentInParent, not GetComponent — matches
            // RoomExitTrigger's pattern for finding a player's networked
            // components from whichever collider actually reported the hit.
            NetworkPlayerEntity targetEntity = hit.GetComponentInParent<NetworkPlayerEntity>();
            NetworkPlayerController targetController = hit.GetComponentInParent<NetworkPlayerController>();
            if (targetEntity == null || targetController == null) continue;

            // Self-exclusion must happen AFTER resolving the entity, not via
            // hit.gameObject == gameObject — the killer's own hitbox collider
            // lives on a child object, not the root, so that comparison never
            // matched and let self-hits through whenever OverlapSphere happened
            // to return the killer's own collider before the real target's.
            if (targetEntity == _entity) continue;
            if (!targetEntity.IsAlive) continue;

            if (targetEntity.IsProtected)
            {
                // Security block — does NOT set HasKilledThisRound, per the
                // design doc. Killer can try a different target this round.
                NotifyKillFailedRpc();
                break;
            }

            ExecuteKill(targetEntity, targetController);
            _entity.SetHasKilledThisRound(true);
            break;
        }
    }

    private void ExecuteKill(NetworkPlayerEntity targetEntity, NetworkPlayerController targetController)
    {
        PlayerRole victimRole = targetEntity.Role;
        int roomIndex = targetEntity.CurrentRoomIndex;

        targetEntity.MarkDead();
        MaskManager.ReassignKillerMask(_entity, victimRole);

        GameEventManager.RaisePlayerKilled(targetController.ClientId, _controller.ClientId, roomIndex);

        CheckWinCondition();
    }

    // Killer wins at 2 players remaining, per the design doc — checked
    // right after every kill resolves rather than on a separate timer.
    private void CheckWinCondition()
    {
        int aliveCount = 0;
        foreach (NetworkPlayerController player in PhaseContext.GetAllPlayersStatic())
        {
            if (player.IsAlive) aliveCount++;
        }

        if (aliveCount <= 2)
        {
            PhaseManager.Instance?.TriggerGameOver("The Killer reduced the manor to 2 survivors.");
        }
    }

    // TODO: hook to a HUD flash/sound once one exists — for now just a
    // console signal so the block-doesn't-consume-attempt rule is
    // verifiable during testing.
    [Rpc(SendTo.Owner)]
    private void NotifyKillFailedRpc()
    {
        Debug.Log("[NetworkPlayerAttack] Kill attempt blocked — try a different target this round.");
    }
}
