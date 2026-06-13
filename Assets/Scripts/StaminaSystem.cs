using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class StaminaSystem : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float regenPerSecond = 1f;

    [Header("Debug Display")]
    [SerializeField] private Slider staminaSlider;

    private NetworkVariable<float> _stamina = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public float Current => _stamina.Value;

    public void Drain(float amount)
    {
        if (!IsOwner) return;
        _stamina.Value = Mathf.Max(0f, _stamina.Value - amount);
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Regen
        if (_stamina.Value < maxStamina)
            _stamina.Value = Mathf.Min(maxStamina, _stamina.Value + regenPerSecond * Time.deltaTime);

        // UI Slider Processing
        if (staminaSlider != null)
        {
            staminaSlider.maxValue = maxStamina;
            staminaSlider.value = _stamina.Value;
        }
    }
}