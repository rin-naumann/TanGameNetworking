using System;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

// Owns Relay allocation and Netcode host/client startup — nothing else should call StartHost/StartClient.
public class ConnectionManager : MonoBehaviour
{
    [Header("Menu UI")]
    [SerializeField] private GameObject multiplayerMenuUI;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_Text statusText;

    [Header("Relay Settings")]
    [Tooltip("4-8 players supported by design.")]
    [SerializeField, Range(4, 8)] private int maxConnections = 8;

    [Header("Handoff")]
    [Tooltip("Told to show its UI and receive the join code once the local connection is confirmed.")]
    [SerializeField] private LobbyReadyManager lobbyReadyManager;

    [Header("Developer Test Mode")]
    [Tooltip("Bypasses Relay entirely and connects via localhost — no Unity Services, " +
             "no join code, no waiting on allocation. Pairs with Multiplayer Play Mode's " +
             "virtual players for cheap 2-instance testing. Also overrides " +
             "LobbyReadyManager's min-players-to-start down to 2. Turn OFF before any " +
             "real playtest with 4-8 players over Relay.")]
    [SerializeField] private bool devTestMode = false;
    [SerializeField] private ushort devTestPort = 7777;
    [SerializeField] private string devTestIp = "127.0.0.1";

    private const string WebGLConnectionType = "wss";
    private string _pendingJoinCode;

    private async void Start()
    {
        await InitializeUnityServices();
        SetStatus("Ready.");
    }

    private async System.Threading.Tasks.Task InitializeUnityServices()
    {
        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }
        catch (Exception exception)
        {
            SetStatus("Unity Services failed to initialize.");
            Debug.LogError(exception);
        }
    }

    public async void StartHost()
    {
        if (devTestMode)
        {
            StartHostLocal();
            return;
        }

        try
        {
            SetStatus("Creating host...");

            await InitializeUnityServices();

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.UseWebSockets = true;
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, WebGLConnectionType));

            _pendingJoinCode = joinCode;

            // Wait for the local connection to actually confirm before flipping the UI.
            NetworkManager.Singleton.OnClientConnectedCallback += HandleLocalClientConnected;

            bool started = NetworkManager.Singleton.StartHost();

            if (!started)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleLocalClientConnected;
                SetStatus("Failed to start Host.");
            }
        }
        catch (Exception exception)
        {
            SetStatus("Host failed. Check Console.");
            Debug.LogError(exception);
        }
    }

    public async void StartClient()
    {
        if (devTestMode)
        {
            StartClientLocal();
            return;
        }

        try
        {
            SetStatus("Joining...");

            await InitializeUnityServices();

            if (joinCodeInput == null)
            {
                SetStatus("Join Code Input is missing.");
                return;
            }

            string joinCode = joinCodeInput.text.Trim().ToUpper();

            if (string.IsNullOrEmpty(joinCode))
            {
                SetStatus("Please enter a join code.");
                return;
            }

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.UseWebSockets = true;
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, WebGLConnectionType));

            NetworkManager.Singleton.OnClientConnectedCallback += HandleLocalClientConnected;

            bool started = NetworkManager.Singleton.StartClient();

            if (!started)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleLocalClientConnected;
                SetStatus("Failed to start Client.");
            }
        }
        catch (Exception exception)
        {
            SetStatus("Client failed. Check join code and Console.");
            Debug.LogError(exception);
        }
    }

    // --- Developer Test Mode: localhost only, no Relay, no Unity Services ---

    private void StartHostLocal()
    {
        SetStatus("[DEV MODE] Starting local host...");

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            SetStatus("[DEV MODE] No UnityTransport on NetworkManager.");
            return;
        }
        transport.SetConnectionData(devTestIp, devTestPort);

        NetworkManager.Singleton.OnClientConnectedCallback += HandleLocalClientConnected;

        bool started = NetworkManager.Singleton.StartHost();
        if (!started)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleLocalClientConnected;
            SetStatus("[DEV MODE] Failed to start local Host.");
            return;
        }

        // Dev mode only needs 2 players, so lower the floor for this session.
        if (lobbyReadyManager != null)
        {
            lobbyReadyManager.SetMinPlayersOverride(2);
        }
    }

    private void StartClientLocal()
    {
        SetStatus("[DEV MODE] Joining local host...");

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            SetStatus("[DEV MODE] No UnityTransport on NetworkManager.");
            return;
        }
        transport.SetConnectionData(devTestIp, devTestPort);

        NetworkManager.Singleton.OnClientConnectedCallback += HandleLocalClientConnected;

        bool started = NetworkManager.Singleton.StartClient();
        if (!started)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleLocalClientConnected;
            SetStatus("[DEV MODE] Failed to start local Client.");
        }
    }

    // Fires for every connecting client on every machine; only act on our own.
    private void HandleLocalClientConnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= HandleLocalClientConnected;

        SetStatus(NetworkManager.Singleton.IsHost ? "Host started. Share the join code." : "Client started.");

        if (lobbyReadyManager != null)
        {
            if (NetworkManager.Singleton.IsHost && !string.IsNullOrEmpty(_pendingJoinCode))
            {
                lobbyReadyManager.SetJoinCode(_pendingJoinCode);
            }
            lobbyReadyManager.ShowLobbyUI();
        }

        if (multiplayerMenuUI != null)
        {
            multiplayerMenuUI.SetActive(false);
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log(message);
    }
}