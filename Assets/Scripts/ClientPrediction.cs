using UnityEngine;
using Unity.Netcode;

public class ClientPrediction : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private float correctionThreshold = 2f;
    [SerializeField] private float snapThreshold = 8f;
    [SerializeField] private float smoothCorrectionSpeed = 10f;

    private CharacterController _cc;

    // The server broadcasts its authoritative position via this NetworkVariable.
    // Server writes, everyone reads.
    private NetworkVariable<Vector3> _serverPosition = new NetworkVariable<Vector3>(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        _cc = GetComponent<CharacterController>();

        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        _serverPosition.OnValueChanged += OnServerPositionChanged;
    }

    // Server samples the player's position periodically and broadcasts it.
    // Since NetworkTransform is client authoritative, the server receives the
    // client's position automatically — we just need to echo it back for validation.
    private void FixedUpdate()
    {
        if (!IsServer) return;

        // Server continuously records the position it sees for this player.
        _serverPosition.Value = transform.position;
    }

    private void OnServerPositionChanged(Vector3 prev, Vector3 current)
    {
        // Only the owner needs to reconcile.
        if (!IsOwner) return;
        ReconcilePosition(current);
    }

    private void ReconcilePosition(Vector3 serverPos)
    {
        float delta = Vector3.Distance(transform.position, serverPos);

        // Within acceptable range — trust the client.
        if (delta < correctionThreshold) return;

        // Large divergence — snap immediately to avoid a long visible slide.
        if (delta >= snapThreshold)
        {
            TeleportTo(serverPos);
            return;
        }

        // Small drift — smooth correction so it's not visually jarring.
        Vector3 corrected = Vector3.Lerp(transform.position, serverPos, smoothCorrectionSpeed * Time.deltaTime);
        TeleportTo(corrected);
    }

    private void TeleportTo(Vector3 position)
    {
        // CharacterController must be disabled to move transform directly.
        _cc.enabled = false;
        transform.position = position;
        _cc.enabled = true;
    }
}
