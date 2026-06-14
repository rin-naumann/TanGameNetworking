using System;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class MultiplayerMenu : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject menuUI;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_Text statusText;

    [Header("Relay Settings")]
    [SerializeField] private int maxConnections = 2; // Fixed connection allocation slot limit

    private const string WebGLConnectionType = "wss";

    private async void Start()
    {
        await InitializeUnityServices();
        
        // Listen for connection events to safely swap menus ONLY after successful handshake
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleLocalClientConnected;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleLocalClientConnected;
        }
    }

    private async System.Threading.Tasks.Task InitializeUnityServices()
    {
        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            SetStatus("Unity Services ready.");
        }
        catch (Exception exception)
        {
            SetStatus("Unity Services failed to initialize.");
            Debug.LogError(exception);
        }
    }

    public async void StartHost()
    {
        try
        {
            SetStatus("Creating host session...");
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.UseWebSockets = true;
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, WebGLConnectionType));

            bool started = NetworkManager.Singleton.StartHost();

            if (started)
            {
                SetStatus("Host started successfully.");
                LobbyManager lobby = FindFirstObjectByType<LobbyManager>();
                if (lobby != null)
                {
                    lobby.SetJoinCode(joinCode);
                    lobby.ShowLobbyUI();
                }
                HideMenu();
            }
            else
            {
                SetStatus("Failed to start Host initialization.");
            }
        }
        catch (Exception exception)
        {
            SetStatus("Host creation failed.");
            Debug.LogError(exception);
        }
    }

    public async void StartClient()
    {
        try
        {
            string joinCode = joinCodeInput.text.Trim();
            if (string.IsNullOrEmpty(joinCode))
            {
                SetStatus("Please enter a valid Join Code.");
                return;
            }

            SetStatus("Connecting to Relay session...");
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.UseWebSockets = true;
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, WebGLConnectionType));

            // StartClient handles connection asynchronously over the web. Do not change UI here!
            NetworkManager.Singleton.StartClient();
            SetStatus("Connecting to host...");
        }
        catch (Exception exception)
        {
            SetStatus("Client connection failed. Verification error.");
            Debug.LogError(exception);
        }
    }

    private void HandleLocalClientConnected(ulong clientId)
    {
        // Fires on the joining machine when its individual connection handshake succeeds
        if (NetworkManager.Singleton.IsServer) return; // Host handles UI inside StartHost directly

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            SetStatus("Connection Confirmed!");
            LobbyManager lobby = FindFirstObjectByType<LobbyManager>();
            if (lobby != null)
            {
                lobby.ShowLobbyUI();
            }
            HideMenu();
        }
    }

    private void HideMenu()
    {
        if (menuUI != null) menuUI.SetActive(false);
    }

    private void SetStatus(string message)
    {
        Debug.Log(message);
        if (statusText != null) statusText.text = message;
    }
}