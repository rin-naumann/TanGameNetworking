using Unity.Netcode;
using UnityEngine;

public class StaminaSystem : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float regenPerSecond = 1f;

    private NetworkVariable<float> _stamina = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public float Current => _stamina.Value;
    public float Max     => maxStamina;

    // Reduces stamina by amount. Only the owning client may write.
    public void Drain(float amount)
    {
        if (!IsOwner) return;
        _stamina.Value = Mathf.Max(0f, _stamina.Value - amount);
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (_stamina.Value < maxStamina)
            _stamina.Value = Mathf.Min(maxStamina, _stamina.Value + regenPerSecond * Time.deltaTime);
    }
}