using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class LobbyManager : NetworkBehaviour
{
    [Header("Lobby UI References")]
    [SerializeField] private GameObject lobbyUI;
    [SerializeField] private TMP_Text lobbyStatusText;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private TMP_Text joinCodeText;
    [SerializeField] private GameObject readyButton;

    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GameObject multiplayerMenuUI;
    [SerializeField] private string spawnPointTag = "SpawnPoint";

    private NetworkVariable<bool> _p1Ready = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> _p2Ready = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> _connectedCount = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> _matchStarted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool _localReady = false;
    private bool _countdownRunning = false;

    public override void OnNetworkSpawn()
    {
        _p1Ready.OnValueChanged        += OnReadyStateChanged;
        _p2Ready.OnValueChanged        += OnReadyStateChanged;
        _matchStarted.OnValueChanged   += OnMatchStarted;
        _connectedCount.OnValueChanged += OnConnectedCountChanged;

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback  += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            _connectedCount.Value = NetworkManager.Singleton.ConnectedClientsList.Count;
        }

        UpdateLobbyUI();
    }

    public override void OnDestroy()
    {
        if (NetworkManager.Singleton == null) return;
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback  -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        _connectedCount.Value = NetworkManager.Singleton.ConnectedClientsList.Count;
        UpdateLobbyClientRpc();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        _connectedCount.Value = Mathf.Max(0, NetworkManager.Singleton.ConnectedClientsList.Count - 1);

        if (!_matchStarted.Value)
        {
            var ids = NetworkManager.Singleton.ConnectedClientsIds;
            if (ids.Count > 0 && clientId == ids[0])
                _p1Ready.Value = false;
            else
                _p2Ready.Value = false;

            _countdownRunning = false;
            StopAllCoroutines();
            UpdateLobbyClientRpc();
        }
        else
        {
            ReturnToMenuClientRpc();
        }
    }

    [ClientRpc]
    private void UpdateLobbyClientRpc() => UpdateLobbyUI();

    public void OnReadyButtonPressed()
    {
        if (_localReady) return;
        _localReady = true;
        if (readyButton != null) readyButton.SetActive(false);
        SetReadyServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SetReadyServerRpc(ulong senderId)
    {
        var ids = NetworkManager.Singleton.ConnectedClientsIds;

        if (ids.Count > 0 && senderId == ids[0])
            _p1Ready.Value = true;
        else
            _p2Ready.Value = true;

        if (_connectedCount.Value >= 2 && _p1Ready.Value && _p2Ready.Value && !_countdownRunning)
        {
            _countdownRunning = true;
            StartCountdownClientRpc();
        }
    }

    private void OnReadyStateChanged(bool prev, bool current) => UpdateLobbyUI();

    // Single correctly-typed overload — int NetworkVariable fires int callbacks.
    private void OnConnectedCountChanged(int prev, int current) => UpdateLobbyUI();

    private void UpdateLobbyUI()
    {
        if (lobbyStatusText == null) return;
        int readyCount = (_p1Ready.Value ? 1 : 0) + (_p2Ready.Value ? 1 : 0);
        lobbyStatusText.text = $"{readyCount} / {_connectedCount.Value} players ready";
    }

    [ClientRpc]
    private void StartCountdownClientRpc()
    {
        StartCoroutine(CountdownRoutine());
    }

    private IEnumerator CountdownRoutine()
    {
        if (lobbyUI != null)       lobbyUI.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(true);

        for (int i = 3; i > 0; i--)
        {
            if (countdownText != null) countdownText.text = i.ToString();
            yield return new WaitForSeconds(1f);
        }

        if (countdownText != null)
        {
            countdownText.text = "GO!";
            yield return new WaitForSeconds(0.5f);
            countdownText.gameObject.SetActive(false);
        }

        if (IsServer) BeginMatch();
    }

    // Server-only. Teleports each player to a spawn point and tells their client to initialize.
    private void BeginMatch()
    {
        _matchStarted.Value = true;

        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag(spawnPointTag);
        var clients = NetworkManager.Singleton.ConnectedClientsList;

        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i].PlayerObject == null) continue;
            Vector3 spawnPos = spawnPoints.Length > 0
                ? spawnPoints[i % spawnPoints.Length].transform.position
                : Vector3.zero;
            TeleportPlayerClientRpc(clients[i].ClientId, spawnPos);
        }
    }

    // Fires on all clients but only runs on the matching player's machine.
    // Teleports the local player, re-enables PlayerController, binds HUD, and activates camera.
    [ClientRpc]
    private void TeleportPlayerClientRpc(ulong clientId, Vector3 position)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId) return;

        var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
        if (localPlayer == null) return;

        // Teleport.
        CharacterController cc = localPlayer.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        localPlayer.transform.position = position;
        if (cc != null) cc.enabled = true;

        // Re-enable PlayerController which was disabled for non-owners on the remote machine.
        // On the owning client it was never disabled, but enabling it again is harmless.
        PlayerController pc = localPlayer.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = true;

        CombatController combat = localPlayer.GetComponent<CombatController>();
        if (combat != null) combat.ActivatePlayer();

        GrappleController grapple = localPlayer.GetComponent<GrappleController>();
        if (grapple != null) grapple.Activate();

        PlayerController pc2 = localPlayer.GetComponent<PlayerController>();
        if (pc2 != null) pc2.Activate();

        // Bind HUD to this player's components.
        HUDController hud = FindFirstObjectByType<HUDController>();
        if (hud != null)
        {
            StaminaSystem stamina = localPlayer.GetComponent<StaminaSystem>();
            hud.BindToLocalPlayer(stamina, combat);
        }

        // Point camera at this player and activate look.
        CameraController cam = Camera.main?.GetComponent<CameraController>();
        if (cam != null)
        {
            cam.SetTarget(localPlayer.transform);
            cam.Activate();
        }

        // Activate GameManager once both players are set up.
        if (gameManager != null) gameManager.Activate();
    }

    // Fires when _matchStarted changes — no longer used for activation, kept for safety.
    private void OnMatchStarted(bool prev, bool current) { }

    public void OnBackToMenuPressed()
    {
        Time.timeScale = 1f;
        NetworkManager.Singleton.Shutdown();
        ReturnToMenu();
    }

    public void OnNewGamePressed()
    {
        Time.timeScale = 1f;
        if (IsHost)
        {
            gameManager?.ResetMatch();
            ResetMatchServerSide();
            ResetLobbyClientRpc();
        }
        else
        {
            RequestNewGameServerRpc();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestNewGameServerRpc()
    {
        gameManager?.ResetMatch();
        ResetMatchServerSide();
        ResetLobbyClientRpc();
    }

    // Resets server-side ready/match state. Does NOT despawn players —
    // NGO won't respawn them automatically, so we keep them alive and just reset their state.
    private void ResetMatchServerSide()
    {
        _p1Ready.Value      = false;
        _p2Ready.Value      = false;
        _matchStarted.Value = false;
        _countdownRunning   = false;
    }

    [ClientRpc]
    private void ResetLobbyClientRpc()
    {
        _localReady = false;

        if (lobbyUI != null)       lobbyUI.SetActive(true);
        if (readyButton != null)   readyButton.SetActive(true);
        if (countdownText != null) countdownText.gameObject.SetActive(false);

        HUDController hud = Object.FindFirstObjectByType<HUDController>();
        if (hud != null)
        {
            hud.HideWinOverlay();
            hud.DeactivateHUD();
        }

        UpdateLobbyUI();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        CameraController cam = Camera.main?.GetComponent<CameraController>();
        if (cam != null) cam.Deactivate();
    }

    [ClientRpc]
    private void ReturnToMenuClientRpc() => ReturnToMenu();

    public void ShowLobbyUI()
    {
        if (lobbyUI != null)           lobbyUI.SetActive(true);
        if (multiplayerMenuUI != null) multiplayerMenuUI.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    public void SetJoinCode(string code)
    {
        if (joinCodeText != null) joinCodeText.text = "Join Code: " + code;
    }

    private void ReturnToMenu()
    {
        HUDController hud = Object.FindFirstObjectByType<HUDController>();
        if (hud != null) hud.HideWinOverlay();

        if (lobbyUI != null)           lobbyUI.SetActive(false);
        if (multiplayerMenuUI != null) multiplayerMenuUI.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        Time.timeScale   = 1f;
    }
}