using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text localScoreText;
    [SerializeField] private TMP_Text enemyScoreText;
    [SerializeField] private TMP_Text killFeedText;
    [SerializeField] private TMP_Text winOverlayText;
    [SerializeField] private GameObject winOverlay;
    [SerializeField] private RectTransform enemyIndicator;

    [Header("Settings")]
    [SerializeField] private float matchDuration = 300f;
    [SerializeField] private float killFeedDuration = 3f;
    [SerializeField] private float indicatorEdgePadding = 40f;

    // Networked state
    private NetworkVariable<float> _timeRemaining = new NetworkVariable<float>(
        300f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> _p1Score = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> _p2Score = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> _isSuddenDeath = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> _isMatchOver = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Transform _enemyTransform;
    private Coroutine _killFeedCoroutine;
    private bool _isActive = false;

    public void Activate() => _isActive = true;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            _timeRemaining.Value = matchDuration;

        _timeRemaining.OnValueChanged += OnTimerChanged;
        _p1Score.OnValueChanged       += OnScoreChanged;
        _p2Score.OnValueChanged       += OnScoreChanged;
        _isSuddenDeath.OnValueChanged += OnSuddenDeathChanged;
        _isMatchOver.OnValueChanged   += OnMatchOverChanged;

        if (winOverlay != null) winOverlay.SetActive(false);
        if (enemyIndicator != null) enemyIndicator.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (IsServer && !_isMatchOver.Value && _isActive) TickTimer();

        UpdateEnemyIndicator();
        UpdateScoreUI();
    }

    private void TickTimer()
    {
        if (_timeRemaining.Value <= 0f)
        {
            _timeRemaining.Value = 0f;

            if (_p1Score.Value == _p2Score.Value)
                _isSuddenDeath.Value = true;
            else
                EndMatch();

            return;
        }

        _timeRemaining.Value -= Time.deltaTime;
    }

    private void OnTimerChanged(float prev, float current)
    {
        if (timerText == null) return;
        int minutes = Mathf.FloorToInt(current / 60f);
        int seconds = Mathf.FloorToInt(current % 60f);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    public void RegisterKill(ulong killerClientId)
    {
        if (!IsServer) return;

        if (killerClientId == NetworkManager.Singleton.ConnectedClientsIds[0])
            _p1Score.Value++;
        else
            _p2Score.Value++;

        NotifyKillClientRpc(killerClientId);

        if (_isSuddenDeath.Value)
            EndMatch();
    }

    private void EndMatch()
    {
        _isMatchOver.Value = true;

        ulong winnerId;
        if (_p1Score.Value > _p2Score.Value)
            winnerId = NetworkManager.Singleton.ConnectedClientsIds[0];
        else if (_p2Score.Value > _p1Score.Value)
            winnerId = NetworkManager.Singleton.ConnectedClientsIds[1];
        else
            winnerId = ulong.MaxValue; // draw, shouldn't happen in sudden death

        ShowWinnerClientRpc(winnerId);
    }

    private void OnScoreChanged(int prev, int current)
    {
        UpdateScoreUI();
    }

    private void UpdateScoreUI()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

        ulong localId = NetworkManager.Singleton.LocalClientId;
        bool isP1 = localId == NetworkManager.Singleton.ConnectedClientsIds[0];

        if (localScoreText != null)
            localScoreText.text = "You: " + (isP1 ? _p1Score.Value : _p2Score.Value);
        if (enemyScoreText != null)
            enemyScoreText.text = "Enemy: " + (isP1 ? _p2Score.Value : _p1Score.Value);
    }

    [ClientRpc]
    private void NotifyKillClientRpc(ulong killerClientId)
    {
        bool localKill = killerClientId == NetworkManager.Singleton.LocalClientId;
        string message = localKill ? "You got a kill!" : "Enemy got a kill!";

        if (_killFeedCoroutine != null)
            StopCoroutine(_killFeedCoroutine);

        _killFeedCoroutine = StartCoroutine(ShowKillFeed(message));
    }

    private IEnumerator ShowKillFeed(string message)
    {
        if (killFeedText == null) yield break;
        killFeedText.text = message;
        killFeedText.gameObject.SetActive(true);
        yield return new WaitForSeconds(killFeedDuration);
        killFeedText.gameObject.SetActive(false);
    }

    private void OnSuddenDeathChanged(bool prev, bool current)
    {
        if (!current) return;

        // Show kill feed message for sudden death.
        if (_killFeedCoroutine != null)
            StopCoroutine(_killFeedCoroutine);
        _killFeedCoroutine = StartCoroutine(ShowKillFeed("Sudden Death! Next kill wins!"));

        // Enable enemy indicator.
        if (enemyIndicator != null)
            enemyIndicator.gameObject.SetActive(true);

        FindEnemyTransform();
    }

    private void FindEnemyTransform()
    {
        ulong localId = NetworkManager.Singleton.LocalClientId;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId == localId) continue;
            if (client.PlayerObject != null)
            {
                _enemyTransform = client.PlayerObject.transform;
                break;
            }
        }
    }

    private void UpdateEnemyIndicator()
    {
        if (!_isSuddenDeath.Value) return;
        if (enemyIndicator == null || _enemyTransform == null) return;
        if (Camera.main == null) return;

        // Project enemy world position to screen space.
        Vector3 screenPos = Camera.main.WorldToScreenPoint(_enemyTransform.position);

        // Flip if behind camera.
        if (screenPos.z < 0)
        {
            screenPos.x = Screen.width - screenPos.x;
            screenPos.y = Screen.height - screenPos.y;
        }

        // Clamp to screen edges with padding.
        float minX = indicatorEdgePadding;
        float maxX = Screen.width  - indicatorEdgePadding;
        float minY = indicatorEdgePadding;
        float maxY = Screen.height - indicatorEdgePadding;

        screenPos.x = Mathf.Clamp(screenPos.x, minX, maxX);
        screenPos.y = Mathf.Clamp(screenPos.y, minY, maxY);

        enemyIndicator.position = screenPos;

        // Rotate indicator to point toward enemy.
        Vector3 direction = _enemyTransform.position - Camera.main.transform.position;
        Vector2 screenDir = Camera.main.WorldToScreenPoint(
            Camera.main.transform.position + direction).normalized;
        float angle = Mathf.Atan2(screenDir.y, screenDir.x) * Mathf.Rad2Deg;
        enemyIndicator.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    [ClientRpc]
    private void ShowWinnerClientRpc(ulong winnerClientId)
    {
        bool localWin = winnerClientId == NetworkManager.Singleton.LocalClientId;
        string message = localWin ? "You Win!" : "You Lose!";

        if (winOverlayText != null) winOverlayText.text = message;
        if (winOverlay != null)     winOverlay.SetActive(true);

        // Pause the game locally.
        Time.timeScale = 0f;

        // Unlock cursor for menu interaction.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    private void OnMatchOverChanged(bool prev, bool current) { }
}