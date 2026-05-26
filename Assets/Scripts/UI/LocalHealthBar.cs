using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using TMPro;

public class LocalHealthBar : MonoBehaviour
{
    public static LocalHealthBar Instance { get; private set; }

    [SerializeField] private Slider healthBar;
    [SerializeField] private GameObject UIObject;
    [SerializeField] private TextMeshProUGUI healthText;
    private NetworkPlayerHealth localPlayerHealth;

    void Awake()
    {
        if (Instance == null) Instance = this; else Destroy(gameObject);
        UIObject.SetActive(false);
    }

    public void BindToPlayer(NetworkPlayerHealth playerHealth)
    {
        
        if (localPlayerHealth != null) localPlayerHealth.currentHealth.OnValueChanged -= OnHealthChanged;
        UIObject.SetActive(true);
        localPlayerHealth = playerHealth;
        healthBar.maxValue = localPlayerHealth.maxHealth;
        healthBar.value = localPlayerHealth.currentHealth.Value;
        healthText.text = $"{localPlayerHealth.currentHealth.Value} / {localPlayerHealth.maxHealth}";
        localPlayerHealth.currentHealth.OnValueChanged += OnHealthChanged;
    }

    private void OnHealthChanged(int previousVal, int newVal)
    {
        healthBar.value = newVal;
        healthText.text = $"{newVal} / {localPlayerHealth.maxHealth}";
    }

    private void OnDestroy()
    {
        if (localPlayerHealth != null) localPlayerHealth.currentHealth.OnValueChanged -= OnHealthChanged;
    }
}
