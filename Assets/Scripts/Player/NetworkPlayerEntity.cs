using UnityEngine;
using Unity.Netcode;

// Holds a player's game-relevant DATA — role, mask, current room — as
// opposed to NetworkPlayerController, which owns physical movement/camera.
// Future per-role ability scripts (DetectiveRole, SecurityRole, ...) read
// and write through this, not through the controller.
public class NetworkPlayerEntity : NetworkBehaviour
{
    private readonly NetworkVariable<PlayerRole> _role =
        new NetworkVariable<PlayerRole>(PlayerRole.Visitor, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
    public PlayerRole Role => _role.Value;
    private readonly NetworkVariable<bool> _isKiller =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
    public bool IsKiller => _isKiller.Value;
    private readonly NetworkVariable<PlayerRole> _currentMaskId =
        new NetworkVariable<PlayerRole>(PlayerRole.Visitor, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public PlayerRole CurrentMaskId => _currentMaskId.Value;
    public event System.Action<PlayerRole> MaskChanged;
    private bool _roleAssigned = false;
    private readonly NetworkVariable<bool> _isAlive =
        new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public bool IsAlive => _isAlive.Value;
    public bool IsProtected { get; private set; }
    public bool HasKilledThisRound { get; private set; }
    public PlayerRoleBehaviour CurrentRole { get; private set; }

    // Fires on every client (read permission is Everyone) the moment
    // _isAlive flips to false — this is what NetworkPlayerController hooks
    // to stop movement/attack for a corpse instead of just the server-side
    // data changing invisibly.
    public event System.Action Died;

    public void SetCurrentRole(PlayerRoleBehaviour role)
    {
        if (!IsServer) return;
        CurrentRole = role;
    }

    public void SetProtected(bool isProtected)
    {
        if (!IsServer) return;
        IsProtected = isProtected;
    }

    public void SetHasKilledThisRound(bool hasKilled)
    {
        if (!IsServer) return;
        HasKilledThisRound = hasKilled;
    }

    public void ResetRoundState()
    {
        if (!IsServer) return;
        HasKilledThisRound = false;
        IsProtected = false;
    }

    public void MarkDead()
    {
        if (!IsServer) return;
        _isAlive.Value = false;
    }

    private readonly NetworkVariable<int> _currentRoomIndex =
        new NetworkVariable<int>(-1, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
    public int CurrentRoomIndex => _currentRoomIndex.Value;
    public event System.Action<int> RoomChanged;

    public override void OnNetworkSpawn()
    {
        _currentRoomIndex.OnValueChanged += (_, newValue) => RoomChanged?.Invoke(newValue);
        _currentMaskId.OnValueChanged += (_, newValue) => MaskChanged?.Invoke(newValue);
        _isAlive.OnValueChanged += (_, isAlive) => { if (!isAlive) Died?.Invoke(); };

        if (IsServer)
        {
            GameEventManager.PlayerKilled += HandlePlayerKilled;
            GameEventManager.PlayerVotedOut += HandlePlayerVotedOut;
        }

    }

    public void AssignRole(PlayerRole role, bool isKiller)
    {
        if (!IsServer) return;
        if (_roleAssigned)
        {
            Debug.LogWarning($"[NetworkPlayerEntity] AssignRole called again for client " +
                              $"{OwnerClientId} — ignored. Role is write-once.");
            return;
        }
        _roleAssigned = true;

        _role.Value = role;
        _isKiller.Value = isKiller;
        _currentMaskId.Value = isKiller ? PlayerRole.Visitor : role;
    }

    public void SetMaskToVictimRole(PlayerRole victimRole)
    {
        if (!IsServer) return;
        _currentMaskId.Value = victimRole;
    }

    public void SetCurrentRoom(int newRoomIndex)
    {
        if (!IsServer) return;
        _currentRoomIndex.Value = newRoomIndex;
    }

    [Rpc(SendTo.Owner)]
    public void PushRoleNotificationRpc(string message)
    {
        PlayerHUD.InvokeOrQueue(hud => hud.AddNotification(message));
    }

    // Dedicated delivery for role-ability feedback (Detective body-found,
    // Security protection, etc.) — routes to PlayerHUD's separate Role
    // Event Panel rather than just the general notification feed.
    [Rpc(SendTo.Owner)]
    public void PushRoleEventRpc(string message)
    {
        PlayerHUD.InvokeOrQueue(hud => hud.ShowRoleEvent(message));
    }

    // Sets the persistent role-identifier label ("Killer", "Detective",
    // etc.) — deliberately separate from PushRoleEventRpc above. That one
    // is a transient ability-event callout that auto-hides and mirrors into
    // the notification feed; this one is a one-time, never-hidden identity
    // label, routed to its own PlayerHUD field (SetRoleIdentifier). Called
    // once by RoleManager.AssignRoles right after a player's role is set.
    [Rpc(SendTo.Owner)]
    public void PushRoleIdentifierRpc(string label)
    {
        PlayerHUD.InvokeOrQueue(hud => hud.SetRoleIdentifier(label));
    }

    private void HandlePlayerKilled(ulong victimClientId, ulong killerClientId, int roomIndex)
    {
        if (victimClientId != OwnerClientId) return;
        PushDeathNotificationRpc("You were murdered.");
    }

    private void HandlePlayerVotedOut(ulong victimClientId, int roomIndex)
    {
        if (victimClientId != OwnerClientId) return;
        PushDeathNotificationRpc("You were voted out by the other players.");
    }

    [Rpc(SendTo.Owner)]
    private void PushDeathNotificationRpc(string reason)
    {
        PlayerHUD.InvokeOrQueue(hud => hud.ShowDeathOverlay(reason));
    }
}
