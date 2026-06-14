using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private float matchDuration = 300f;
    [SerializeField] private float killFeedDuration = 3f;
    [SerializeField] private float indicatorEdgePadding = 40f;

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

    private HUDController _hud;
    private LobbyManager _lobby;
    private Transform _enemyTransform;
    private Coroutine _killFeedCoroutine;
    private bool _isActive = false;

    // Called by LobbyManager once countdown ends.
    public void Activate()
    {
        _isActive = true;
        _hud   = FindFirstObjectByType<HUDController>();
        _lobby = FindFirstObjectByType<LobbyManager>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            _timeRemaining.Value = matchDuration;

        _timeRemaining.OnValueChanged += OnTimerChanged;
        _p1Score.OnValueChanged       += OnScoreChanged;
        _p2Score.OnValueChanged       += OnScoreChanged;
        _isSuddenDeath.OnValueChanged += OnSuddenDeathChanged;
    }

    private void Update()
    {
        if (IsServer && _isActive && !_isMatchOver.Value)
            TickTimer();

        if (_isActive && _isSuddenDeath.Value)
            UpdateEnemyIndicator();
    }

    // Ticks the server timer. Triggers sudden death or match end at zero.
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
        if (_hud != null) _hud.UpdateTimer(current);
    }

    // Called by CombatController on the server when a kill is confirmed.
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

    // Determines winner and ends the match.
    private void EndMatch()
    {
        _isMatchOver.Value = true;

        ulong winnerId;
        if (_p1Score.Value > _p2Score.Value)
            winnerId = NetworkManager.Singleton.ConnectedClientsIds[0];
        else if (_p2Score.Value > _p1Score.Value)
            winnerId = NetworkManager.Singleton.ConnectedClientsIds[1];
        else
            winnerId = ulong.MaxValue;

        ShowWinnerClientRpc(winnerId);
    }

    private void OnScoreChanged(int prev, int current)
    {
        if (!NetworkManager.Singleton.IsListening) return;
        if (_hud == null) _hud = FindFirstObjectByType<HUDController>();
        if (_hud == null) return;

        ulong localId = NetworkManager.Singleton.LocalClientId;
        bool isP1 = localId == NetworkManager.Singleton.ConnectedClientsIds[0];

        _hud.UpdateScores(
            isP1 ? _p1Score.Value : _p2Score.Value,
            isP1 ? _p2Score.Value : _p1Score.Value);
    }

    [ClientRpc]
    private void NotifyKillClientRpc(ulong killerClientId)
    {
        if (_hud == null) _hud = FindFirstObjectByType<HUDController>();
        bool localKill = killerClientId == NetworkManager.Singleton.LocalClientId;

        if (_killFeedCoroutine != null) StopCoroutine(_killFeedCoroutine);
        _killFeedCoroutine = StartCoroutine(
            KillFeedRoutine(localKill ? "You got a kill!" : "Enemy got a kill!"));
    }

    private IEnumerator KillFeedRoutine(string message)
    {
        if (_hud == null) yield break;
        _hud.ShowKillFeed(message);
        yield return new WaitForSeconds(killFeedDuration);
        _hud.HideKillFeed();
    }

    private void OnSuddenDeathChanged(bool prev, bool current)
    {
        if (!current) return;
        if (_hud == null) _hud = FindFirstObjectByType<HUDController>();

        if (_killFeedCoroutine != null) StopCoroutine(_killFeedCoroutine);
        _killFeedCoroutine = StartCoroutine(KillFeedRoutine("Sudden Death! Next kill wins!"));

        _hud.ShowEnemyIndicator();
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
        if (_hud == null || _enemyTransform == null || Camera.main == null) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(_enemyTransform.position);

        if (screenPos.z < 0)
        {
            screenPos.x = Screen.width  - screenPos.x;
            screenPos.y = Screen.height - screenPos.y;
        }

        screenPos.x = Mathf.Clamp(screenPos.x, indicatorEdgePadding, Screen.width  - indicatorEdgePadding);
        screenPos.y = Mathf.Clamp(screenPos.y, indicatorEdgePadding, Screen.height - indicatorEdgePadding);

        Vector3 dir      = _enemyTransform.position - Camera.main.transform.position;
        Vector2 screenDir = Camera.main.WorldToScreenPoint(Camera.main.transform.position + dir).normalized;
        float angle      = Mathf.Atan2(screenDir.y, screenDir.x) * Mathf.Rad2Deg;

        _hud.UpdateEnemyIndicator(screenPos, angle - 90f);
    }

    // Fires on all clients to show the win screen with both post-game buttons.
    [ClientRpc]
    private void ShowWinnerClientRpc(ulong winnerClientId)
    {
        if (_hud == null) _hud = FindFirstObjectByType<HUDController>();

        bool localWin = winnerClientId == NetworkManager.Singleton.LocalClientId;
        _hud.ShowWinOverlay(localWin ? "You Win!" : "You Lose!");

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    // Resets all server-side match state for a new game.
    public void ResetMatch()
    {
        if (!IsServer) return;
        _p1Score.Value       = 0;
        _p2Score.Value       = 0;
        _isSuddenDeath.Value = false;
        _isMatchOver.Value   = false;
        _timeRemaining.Value = matchDuration;
        _isActive            = false;
        _enemyTransform      = null;
    }
}