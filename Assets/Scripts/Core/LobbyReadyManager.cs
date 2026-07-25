using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

// Ready-up and match-start logic for 4-8 players.
public class LobbyReadyManager : NetworkBehaviour
{
    [Header("Lobby UI")]
    [SerializeField] private GameObject lobbyUI;
    [SerializeField] private TMP_Text joinCodeText;
    [SerializeField] private TMP_Text playerListText;
    [SerializeField] private TMP_Text readyStatusText;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private GameObject readyButton;

    [Header("Match Rules")]
    [Tooltip("Design floor/ceiling: 4-8 players.")]
    [SerializeField, Range(4, 8)] private int minPlayersToStart = 4;
    [SerializeField] private int countdownSeconds = 3;

    // Server-authoritative source of truth. Client id -> ready state.
    private readonly Dictionary<ulong, bool> _playerReadyStates = new Dictionary<ulong, bool>();

    private bool _localReady = false;
    private bool _countdownRunning = false;

    // Overrides the min-player floor for dev testing; -1 means use the inspector value.
    private int _minPlayersOverride = -1;

    private int EffectiveMinPlayers => _minPlayersOverride > 0 ? _minPlayersOverride : minPlayersToStart;

    public void SetMinPlayersOverride(int count)
    {
        _minPlayersOverride = count;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                _playerReadyStates[clientId] = false;
            }

            readyButton?.SetActive(true);
            PushLobbyStateClientRpc(BuildStateString());
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    // --- Server-side connection tracking ---

    private void HandleClientConnected(ulong clientId)
    {
        if (!_playerReadyStates.ContainsKey(clientId))
        {
            _playerReadyStates[clientId] = false;
        }
        PushLobbyStateClientRpc(BuildStateString());
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        _playerReadyStates.Remove(clientId);

        // A departure mid-countdown invalidates it — don't launch a match short a player.
        if (_countdownRunning)
        {
            _countdownRunning = false;
            StopAllCoroutines();
            CancelCountdownClientRpc();
        }

        PushLobbyStateClientRpc(BuildStateString());
    }

    // --- Ready-up ---

    public void OnReadyButtonPressed()
    {
        if (_localReady) return;
        _localReady = true;
        readyButton?.SetActive(false);
        SetReadyServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SetReadyServerRpc(ulong senderId)
    {
        if (!_playerReadyStates.ContainsKey(senderId)) return;

        _playerReadyStates[senderId] = true;
        PushLobbyStateClientRpc(BuildStateString());

        if (AllPlayersReady() && !_countdownRunning)
        {
            _countdownRunning = true;
            StartCountdownClientRpc();
        }
    }

    private bool AllPlayersReady()
    {
        if (_playerReadyStates.Count < EffectiveMinPlayers) return false;

        foreach (bool ready in _playerReadyStates.Values)
        {
            if (!ready) return false;
        }
        return true;
    }

    // --- UI sync ---

    private string BuildStateString()
    {
        int readyCount = 0;
        foreach (bool ready in _playerReadyStates.Values)
        {
            if (ready) readyCount++;
        }
        // Encoded as "ready|total" to keep this a single RPC instead of multiple NetworkVariables.
        return $"{readyCount}|{_playerReadyStates.Count}";
    }

    [ClientRpc]
    private void PushLobbyStateClientRpc(string state)
    {
        string[] parts = state.Split('|');
        if (readyStatusText != null && parts.Length == 2)
        {
            readyStatusText.text = $"{parts[0]} / {parts[1]} players ready (min {EffectiveMinPlayers})";
        }
    }

    // --- Countdown + match start ---

    [ClientRpc]
    private void StartCountdownClientRpc()
    {
        StartCoroutine(CountdownRoutine());
    }

    [ClientRpc]
    private void CancelCountdownClientRpc()
    {
        StopAllCoroutines();
        if (countdownText != null) countdownText.gameObject.SetActive(false);
        readyButton?.SetActive(!_localReady ? true : readyButton.activeSelf);
    }

    private IEnumerator CountdownRoutine()
    {
        if (countdownText != null) countdownText.gameObject.SetActive(true);

        for (int i = countdownSeconds; i > 0; i--)
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

        if (lobbyUI != null) lobbyUI.SetActive(false);

        // Must run on every client, not just the server, so every player's HUD turns on.
        UIManager.Instance?.ShowMatchUI();

        if (IsServer)
        {
            _countdownRunning = false;
            BeginMatch();
        }
    }

    // Server-only: places every player in a random room, then hands off to PhaseManager.
    private void BeginMatch()
    {
        Debug.Log("[LobbyReadyManager] All players ready — assigning roles and starting the match.");

        if (RoomsManager.Instance == null)
        {
            Debug.LogError("[LobbyReadyManager] No RoomsManager.Instance found — can't start the match.");
            return;
        }

        GameEventManager.ResetForNewMatch();

        List<NetworkPlayerController> controllers = new List<NetworkPlayerController>();
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            NetworkClient client = NetworkManager.Singleton.ConnectedClients[clientId];
            if (client.PlayerObject == null) continue;

            NetworkPlayerController controller = client.PlayerObject.GetComponent<NetworkPlayerController>();
            if (controller != null) controllers.Add(controller);
        }

        RoleManager.AssignRoles(controllers);

        // Rolled once per player so players don't all spawn in the same room.
        foreach (NetworkPlayerController controller in controllers)
        {
            RoomObject spawnRoom = RoomsManager.Instance.GetRandomRoom();
            if (spawnRoom == null)
            {
                Debug.LogError($"[LobbyReadyManager] RoomsManager has no rooms configured — " +
                               $"can't place client {controller.ClientId}.");
                continue;
            }

            spawnRoom.AddOccupant(controller.ClientId);
            controller.NotifyRoomChanged(spawnRoom.RoomIndex, spawnRoom);
            controller.SetMovementEnabled(true);
        }

        PhaseManager.Instance?.BeginMatch();
    }

    public void ShowLobbyUI()
    {
        if (lobbyUI != null) lobbyUI.SetActive(true);
    }

    public void SetJoinCode(string code)
    {
        if (joinCodeText != null) joinCodeText.text = "Join Code: " + code;
    }
}
