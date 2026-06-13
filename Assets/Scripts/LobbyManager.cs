using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class LobbyManager : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject lobbyUI;
    [SerializeField] private TMP_Text lobbyStatusText;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private GameObject readyButton;
    [SerializeField] private GameObject startButton;

    [Header("References")]
    [SerializeField] private GameManager gameStateManager;
    [SerializeField] private string spawnPointTag = "SpawnPoint";

    private NetworkVariable<bool> _p1Ready = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> _p2Ready = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> _matchStarted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool _localReady = false;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public override void OnNetworkSpawn()
    {
        _p1Ready.OnValueChanged      += OnReadyStateChanged;
        _p2Ready.OnValueChanged      += OnReadyStateChanged;
        _matchStarted.OnValueChanged += OnMatchStarted;

        // Start button only visible to host, and only when both are ready.
        if (startButton != null)
            startButton.SetActive(false);

        UpdateLobbyUI();
    }

    // -------------------------------------------------------------------------
    // Ready button
    // -------------------------------------------------------------------------

    public void OnReadyButtonPressed()
    {
        if (_localReady) return;
        _localReady = true;

        if (readyButton != null)
            readyButton.SetActive(false);

        SetReadyServerRpc();
    }

    [ServerRpc]
    private void SetReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        if (senderId == NetworkManager.Singleton.ConnectedClientsIds[0])
            _p1Ready.Value = true;
        else
            _p2Ready.Value = true;
    }

    private void OnReadyStateChanged(bool prev, bool current)
    {
        UpdateLobbyUI();

        // Show start button to host when both are ready.
        if (IsHost && startButton != null)
            startButton.SetActive(_p1Ready.Value && _p2Ready.Value);
    }

    private void UpdateLobbyUI()
    {
        if (lobbyStatusText == null) return;

        int readyCount = (_p1Ready.Value ? 1 : 0) + (_p2Ready.Value ? 1 : 0);
        lobbyStatusText.text = readyCount + " / 2 players ready";
    }

    // -------------------------------------------------------------------------
    // Start button (host only)
    // -------------------------------------------------------------------------

    public void OnStartButtonPressed()
    {
        if (!IsHost) return;
        if (!_p1Ready.Value || !_p2Ready.Value) return;

        StartCountdownClientRpc();
    }

    // -------------------------------------------------------------------------
    // Countdown
    // -------------------------------------------------------------------------

    [ClientRpc]
    private void StartCountdownClientRpc()
    {
        StartCoroutine(CountdownRoutine());
    }

    private IEnumerator CountdownRoutine()
    {
        if (lobbyUI != null)    lobbyUI.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(true);

        for (int i = 3; i > 0; i--)
        {
            if (countdownText != null)
                countdownText.text = i.ToString();
            yield return new WaitForSeconds(1f);
        }

        if (countdownText != null)
        {
            countdownText.text = "GO!";
            yield return new WaitForSeconds(0.5f);
            countdownText.gameObject.SetActive(false);
        }

        // Server triggers the actual match start.
        if (IsServer)
            BeginMatch();
    }

    // -------------------------------------------------------------------------
    // Match start
    // -------------------------------------------------------------------------

    private void BeginMatch()
    {
        _matchStarted.Value = true;

        // Teleport all players to spawn points.
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag(spawnPointTag);

        int index = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;

            Transform spawn = spawnPoints[index % spawnPoints.Length].transform;
            TeleportPlayerClientRpc(client.ClientId, spawn.position);
            index++;
        }
    }

    [ClientRpc]
    private void TeleportPlayerClientRpc(ulong clientId, Vector3 position)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId) return;

        // Find local player object and teleport.
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId != clientId) continue;
            if (client.PlayerObject == null) continue;

            CharacterController cc = client.PlayerObject.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            client.PlayerObject.transform.position = position;
            if (cc != null) cc.enabled = true;
            break;
        }
    }

    private void OnMatchStarted(bool prev, bool current)
    {
        if (!current) return;

        // Unlock shooting on CombatController.
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId != NetworkManager.Singleton.LocalClientId) continue;
            if (client.PlayerObject == null) continue;

            CombatController combat = client.PlayerObject.GetComponent<CombatController>();
            if (combat != null) combat.UnlockCombat();
        }

        // Activate GameStateManager.
        if (gameStateManager != null)
            gameStateManager.Activate();
    }
}