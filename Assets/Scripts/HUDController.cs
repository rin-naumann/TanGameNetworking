using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class HUDController : NetworkBehaviour
{
    [Header("Stamina")]
    [SerializeField] private Slider staminaSlider;

    [Header("Scores")]
    [SerializeField] private TMP_Text localScoreText;
    [SerializeField] private TMP_Text enemyScoreText;

    [Header("Timer")]
    [SerializeField] private TMP_Text timerText;

    [Header("Kill Feed")]
    [SerializeField] private TMP_Text killFeedText;

    [Header("Crosshair")]
    [SerializeField] private RectTransform crosshairDot;
    [SerializeField] private float crosshairSize = 8f;

    [Header("Scope Overlay")]
    [SerializeField] private GameObject scopeOverlay;

    [Header("Win Overlay")]
    [SerializeField] private GameObject winOverlay;
    [SerializeField] private TMP_Text winOverlayText;

    [Header("Enemy Indicator")]
    [SerializeField] private RectTransform enemyIndicator;

    private StaminaSystem _stamina;
    private CombatController _combat;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        // Find local player components.
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId != NetworkManager.Singleton.LocalClientId) continue;
            if (client.PlayerObject == null) continue;

            _stamina = client.PlayerObject.GetComponent<StaminaSystem>();
            _combat  = client.PlayerObject.GetComponent<CombatController>();
            break;
        }

        InitializeCrosshair();
        InitializeHUD();
    }

    private void InitializeCrosshair()
    {
        if (crosshairDot == null) return;
        crosshairDot.sizeDelta = new Vector2(crosshairSize, crosshairSize);
        crosshairDot.anchoredPosition = Vector2.zero;
    }

    private void InitializeHUD()
    {
        if (killFeedText != null)    killFeedText.gameObject.SetActive(false);
        if (winOverlay != null)      winOverlay.SetActive(false);
        if (enemyIndicator != null)  enemyIndicator.gameObject.SetActive(false);
        if (scopeOverlay != null)    scopeOverlay.SetActive(false);

        if (staminaSlider != null)
        {
            staminaSlider.minValue = 0f;
            staminaSlider.maxValue = 100f;
            staminaSlider.value    = 100f;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        UpdateStamina();
        UpdateScopeOverlay();
    }

    private void UpdateStamina()
    {
        if (staminaSlider == null || _stamina == null) return;
        staminaSlider.value = _stamina.Current;
    }

    private void UpdateScopeOverlay()
    {
        if (scopeOverlay == null || _combat == null) return;
        scopeOverlay.SetActive(_combat.IsScoped);
    }

    // -------------------------------------------------------------------------
    // Called by GameStateManager
    // -------------------------------------------------------------------------

    public void UpdateTimer(float timeRemaining)
    {
        if (timerText == null) return;
        int minutes = Mathf.FloorToInt(timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(timeRemaining % 60f);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    public void UpdateScores(int localScore, int enemyScore)
    {
        if (localScoreText != null) localScoreText.text = "You: "    + localScore;
        if (enemyScoreText != null) enemyScoreText.text = "Enemy: "  + enemyScore;
    }

    public void ShowKillFeed(string message)
    {
        if (killFeedText == null) return;
        killFeedText.text = message;
        killFeedText.gameObject.SetActive(true);
    }

    public void HideKillFeed()
    {
        if (killFeedText != null) killFeedText.gameObject.SetActive(false);
    }

    public void ShowWinOverlay(string message)
    {
        if (winOverlayText != null) winOverlayText.text = message;
        if (winOverlay != null)     winOverlay.SetActive(true);
    }

    public void ShowEnemyIndicator()
    {
        if (enemyIndicator != null) enemyIndicator.gameObject.SetActive(true);
    }

    public void UpdateEnemyIndicator(Vector3 screenPos, float angle)
    {
        if (enemyIndicator == null) return;
        enemyIndicator.position = screenPos;
        enemyIndicator.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}