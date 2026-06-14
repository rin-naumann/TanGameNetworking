using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    [Header("PlayerHUD Panel")]
    [SerializeField] private GameObject playerHUD;

    [Header("Stamina UI")]
    [SerializeField] private Slider staminaSlider;

    [Header("Ammo UI")]
    [SerializeField] private TMP_Text ammoText;
    [SerializeField] private TMP_Text reloadingText;

    [Header("Scores UI")]
    [SerializeField] private TMP_Text localScoreText;
    [SerializeField] private TMP_Text enemyScoreText;

    [Header("Timer UI")]
    [SerializeField] private TMP_Text timerText;

    [Header("Kill Feed UI")]
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

    // Cached references to the local player's systems, assigned dynamically
    private StaminaSystem _localStamina;
    private CombatController _localCombat;

    private void Awake()
    {
        // Auto-detect the HUD panel if it wasn't dragged into the inspector
        if (playerHUD == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("HUD");
            if (found != null) playerHUD = found;
            else Debug.LogError("HUDController: Could not find GameObject with tag 'HUD'!");
        }

        InitializeCrosshair();
        InitializeHUD();
        DeactivateHUD();
    }

    /// <summary>
    /// Binds the local player's data streams directly to this UI instance.
    /// Called by the local player's script upon Network Spawn.
    /// </summary>
    public void BindToLocalPlayer(StaminaSystem stamina, CombatController combat)
    {
        _localStamina = stamina;
        _localCombat = combat;
        ActivateHUD();
    }

    public void ActivateHUD()
    {
        if (playerHUD != null)
        {
            playerHUD.SetActive(true);
        }
        else
        {
            Debug.LogError("HUDController: playerHUD panel reference is missing!");
        }
    }

    public void DeactivateHUD()
    {
        if (playerHUD != null) playerHUD.SetActive(false);
    }

    private void InitializeCrosshair()
    {
        if (crosshairDot == null) return;
        crosshairDot.sizeDelta = new Vector2(crosshairSize, crosshairSize);
        crosshairDot.anchoredPosition = Vector2.zero;
    }

    private void InitializeHUD()
    {
        if (killFeedText != null)   killFeedText.gameObject.SetActive(false);
        if (winOverlay != null)     winOverlay.SetActive(false);
        if (enemyIndicator != null) enemyIndicator.gameObject.SetActive(false);
        if (scopeOverlay != null)   scopeOverlay.SetActive(false);
        if (reloadingText != null)  reloadingText.gameObject.SetActive(false);

        if (staminaSlider != null)
        {
            staminaSlider.minValue = 0f;
            staminaSlider.maxValue = 100f;
            staminaSlider.value    = 100f;
        }
    }

    private void Update()
    {
        // If no local player has bound to this HUD yet, don't execute visual updates
        if (_localStamina == null || _localCombat == null) return;

        UpdateStaminaUI();
        UpdateScopeOverlayUI();
        UpdateAmmoUI();
    }

    private void UpdateStaminaUI()
    {
        if (staminaSlider == null) return;
        staminaSlider.value = _localStamina.Current;
    }

    private void UpdateScopeOverlayUI()
    {
        if (scopeOverlay == null) return;
        scopeOverlay.SetActive(_localCombat.IsScoped);
    }

    private void UpdateAmmoUI()
    {
        if (ammoText != null)
            ammoText.text = _localCombat.CurrentAmmo + " / " + _localCombat.MaxAmmo;
        
        if (reloadingText != null)
            reloadingText.gameObject.SetActive(_localCombat.IsReloading);
    }

    public void UpdateTimer(float timeRemaining)
    {
        if (timerText == null) return;
        int minutes = Mathf.FloorToInt(timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(timeRemaining % 60f);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    public void UpdateScores(int localScore, int enemyScore)
    {
        if (localScoreText != null) localScoreText.text = "You: "   + localScore;
        if (enemyScoreText != null) enemyScoreText.text = "Enemy: " + enemyScore;
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

    public void HideWinOverlay()
    {
        if (winOverlay != null) winOverlay.SetActive(false);
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