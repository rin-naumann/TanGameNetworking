using Unity.Netcode;
using UnityEngine;

// Renders CurrentMaskId as a color on the prefab's "Mask" child renderer.
// Purely visual/client-observed — CurrentMaskId itself is already
// server-authoritative and synced (NetworkVariable, ReadPermission.Everyone).
// Runs for every player object on every machine (not owner-gated), so
// everyone always sees everyone else's current mask color, including the
// killer's after MaskManager.ReassignKillerMask reassigns it on a kill.
public class MaskVisual : NetworkBehaviour
{
    [SerializeField] private Renderer maskRenderer;

    private NetworkPlayerEntity _entity;
    private MaterialPropertyBlock _propBlock;

    private void Awake()
    {
        _entity = GetComponent<NetworkPlayerEntity>();
        _propBlock = new MaterialPropertyBlock();
    }

    public override void OnNetworkSpawn()
    {
        _entity.MaskChanged += HandleMaskChanged;
        // MaskChanged only fires on future changes (NetworkVariable.OnValueChanged
        // semantics) - sync to whatever CurrentMaskId already is right now,
        // same pattern GameOverUI/PhaseTimerUI use to cover the "already
        // happened before I subscribed" case.
        HandleMaskChanged(_entity.CurrentMaskId);
    }

    public override void OnNetworkDespawn()
    {
        _entity.MaskChanged -= HandleMaskChanged;
    }

    private void HandleMaskChanged(PlayerRole role)
    {
        if (maskRenderer == null) return;

        maskRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor("_BaseColor", RoleColor.ColorForRole(role));
        _propBlock.SetColor("_Color", RoleColor.ColorForRole(role)); // covers Built-in RP shaders too
        maskRenderer.SetPropertyBlock(_propBlock);
    }

    // Palette now lives in RoleColor (single source of truth), so UI text
    // naming a role (VotingUI, StalkerRole's event push, etc.) tints itself
    // with the exact same color as this mask mesh instead of each keeping
    // its own copy that could drift out of sync.
}
