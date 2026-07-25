using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

// Trimmed from the original: no jump, no gravity — flat manor floor, nothing
// to fall from. Keeps the owner-check + ServerRpc movement pattern as-is,
// since that's exactly the server-authoritative shape we want everywhere else.
//
// Also implements IRoomOccupant so this is the piece that actually closes
// the loop RoomExitTrigger/RoomsManager were built around: physical trigger
// detection (client-side) -> this RPC (reaches the server) ->
// RoomsManager.RequestMoveThroughExit (server-authoritative resolution).
public class NetworkPlayerController : NetworkBehaviour, IRoomOccupant
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Camera")]
    [Tooltip("Eye-height point the camera follows. Defaults to this object's " +
             "own transform if left empty.")]
    [SerializeField] private Transform cameraAnchor;

    private CharacterController _characterController;
    private CameraController _cameraController;
    private NetworkTransform _networkTransform;
    private NetworkPlayerEntity _entity;

    // Server-authoritative, owner-only read — same reasoning as room index
    // on the entity. Gates BOTH movement and camera/cursor activation.
    private readonly NetworkVariable<bool> _movementEnabled =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

    // Thin pass-throughs to NetworkPlayerEntity — data now lives there;
    // callers (LobbyReadyManager, CurrentRoomUI) don't need to change.
    public PlayerRole Role => _entity.Role;
    public PlayerRole CurrentMaskId => _entity.CurrentMaskId;
    public event System.Action<PlayerRole> MaskChanged
    {
        add => _entity.MaskChanged += value;
        remove => _entity.MaskChanged -= value;
    }
    public int CurrentRoomIndex => _entity.CurrentRoomIndex;
    public event System.Action<int> RoomChanged
    {
        add => _entity.RoomChanged += value;
        remove => _entity.RoomChanged -= value;
    }
    public bool IsAlive => _entity.IsAlive;

    // Direct entity access — RoleManager and RoamingPhase need to call
    // entity-level methods (SetCurrentRole, ResetRoundState) that don't
    // have their own pass-through, since nothing else needs them yet.
    public NetworkPlayerEntity Entity => _entity;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _networkTransform = GetComponent<NetworkTransform>();
        _entity = GetComponent<NetworkPlayerEntity>();
    }

    public override void OnNetworkSpawn()
    {
        _movementEnabled.OnValueChanged += HandleMovementEnabledChanged;
        _entity.Died += HandleDeath;

        if (!IsOwner) return;
        LocalPlayer.Register(this);

        _cameraController = FindFirstObjectByType<CameraController>();
        if (_cameraController != null) _cameraController.SetTarget(cameraAnchor != null ? cameraAnchor : transform);
        if (_movementEnabled.Value) _cameraController?.Activate();
    }

    public override void OnNetworkDespawn()
    {
        _movementEnabled.OnValueChanged -= HandleMovementEnabledChanged;
        _entity.Died -= HandleDeath;

        if (!IsOwner) return;

        LocalPlayer.Clear(this);
        _cameraController?.Deactivate();
    }

    private void HandleMovementEnabledChanged(bool previousValue, bool newValue)
    {
        if (!IsOwner) return;

        if (newValue) _cameraController?.Activate();
        else _cameraController?.Deactivate();
    }

    // Runs on every client the moment the entity reports death. Stops the
    // corpse from continuing to walk/turn/attack, and for the owner also
    // drops camera/cursor control back so the death overlay (with its
    // leave-session button) reads clearly instead of fighting for input.
    private void HandleDeath()
    {
        if (_characterController != null) _characterController.enabled = false;

        // Hide the corpse's visuals on every client (this method already runs
        // everywhere via NetworkPlayerEntity.Died, which has Everyone read
        // permission). Disabling the MeshRenderers rather than SetActive-ing
        // the whole GameObject, since this root also hosts NetworkObject/
        // NetworkTransform/NetworkBehaviours -- deactivating a spawned
        // NetworkObject's GameObject client-side isn't safe in Netcode.
        // Covers both the root body mesh and the Mask child.
        foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>())
        {
            renderer.enabled = false;
        }

        if (!IsOwner) return;
        _cameraController?.Deactivate();
    }

    // Server-only. Called by LobbyReadyManager.BeginMatch() once all players
    // are ready — this is the actual "match has started" signal that turns
    // on movement and hands the owner their camera/cursor lock.
    public void SetMovementEnabled(bool enabled)
    {
        if (!IsServer) return;
        _movementEnabled.Value = enabled;
    }

    // --- Movement (owner input -> server-authoritative application) ---

    private void Update()
    {
        if (!IsOwner) return;
        if (!_movementEnabled.Value) return;
        if (!_entity.IsAlive) return;

        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        float yaw = _cameraController != null ? _cameraController.Yaw : transform.eulerAngles.y;
        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 moveDirection = (yawRotation * new Vector3(horizontalInput, 0f, verticalInput)).normalized;

        UpdateMoveDirectionRpc(moveDirection);

        // Send our current facing (tracked locally by CameraController.Yaw) up to
        // the server so it can become the authoritative rotation other clients
        // see via NetworkTransform - previously nothing did this, so only the
        // owner's own camera ever reflected turning; the visible prefab never did.
        UpdateLookYawRpc(yaw);
    }

    [Rpc(SendTo.Server)]
    private void UpdateMoveDirectionRpc(Vector3 moveDirection)
    {
        _pendingMoveDirection = moveDirection;
    }
    private Vector3 _pendingMoveDirection;

    [Rpc(SendTo.Server)]
    private void UpdateLookYawRpc(float yaw)
    {
        _pendingYaw = yaw;
    }
    private float _pendingYaw;

    private void FixedUpdate()
    {
        if (!IsServer) return;
        if (!_entity.IsAlive) return;
        _characterController.Move(_pendingMoveDirection * moveSpeed * Time.fixedDeltaTime);
        transform.rotation = Quaternion.Euler(0f, _pendingYaw, 0f);
    }

    public ulong ClientId => OwnerClientId;
    public bool IsKillerRole => _entity.IsKiller;

    public void AssignRole(PlayerRole role, bool isKiller) => _entity.AssignRole(role, isKiller);

    public void SetMaskToVictimRole(PlayerRole victimRole) => _entity.SetMaskToVictimRole(victimRole);

    public void RequestMoveThroughDoor(RoomObject fromRoom, RoomsManager.Direction direction)
    {
        if (!IsOwner) return;
        RequestMoveThroughDoorRpc(fromRoom.RoomIndex, direction);
    }

    [Rpc(SendTo.Server)]
    private void RequestMoveThroughDoorRpc(int fromRoomIndex, RoomsManager.Direction direction)
    {
        RoomObject fromRoom = RoomsManager.Instance.GetRoomByIndex(fromRoomIndex);
        if (fromRoom == null) return;

        RoomsManager.Instance.RequestMoveThroughExit(fromRoom, direction, this);
    }

    public void NotifyRoomChanged(int newRoomIndex, RoomObject newRoom)
    {
        int previousRoomIndex = _entity.CurrentRoomIndex;
        _entity.SetCurrentRoom(newRoomIndex);

        _characterController.enabled = false;
        if (_networkTransform != null)
        {
            _networkTransform.Teleport(newRoom.EntryPoint.position, transform.rotation, transform.localScale);
        }
        else
        {
            transform.position = newRoom.EntryPoint.position;
        }

        _characterController.enabled = true;

        if (IsServer)
        {
            GameEventManager.RaisePlayerRelocated(ClientId, previousRoomIndex, newRoomIndex);
        }
    }
}
