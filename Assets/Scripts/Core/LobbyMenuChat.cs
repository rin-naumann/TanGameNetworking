using System.Collections;
using System.Linq;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Pure chat component; host/join logic lives in ConnectionManager. Assumes a network session already exists.
public class LobbyMenuChat : NetworkBehaviour
{
    [Header("Chat UI")]
    [SerializeField] private TMP_Text chatDisplayText;
    [SerializeField] private TMP_InputField chatInputField;

    // True only during the Voting Phase; toggled by PhaseManager on every machine.
    public static bool IsChatAllowed = false;

    // Same timing problem VotingUI already solves for LocalPlayer.Controller:
    // LocalPlayer.Controller is set in NetworkPlayerController's own
    // OnNetworkSpawn, which can run after this component's OnEnable. Poll
    // until it exists, then hook the local player's Died event so the input
    // field locks the moment THIS client's player dies, not just next time
    // SendChatMessage happens to be called.
    private NetworkPlayerEntity _subscribedLocalEntity;
    private Coroutine _localDeathSubscribeRoutine;
    private bool _localPlayerDead;

    private void Start()
    {
        if (chatDisplayText != null)
        {
            chatDisplayText.text = "Chat:";
        }
    }

    private void OnEnable()
    {
        _localDeathSubscribeRoutine = StartCoroutine(SubscribeToLocalDeathWhenReady());
    }

    private void OnDisable()
    {
        if (_localDeathSubscribeRoutine != null)
        {
            StopCoroutine(_localDeathSubscribeRoutine);
            _localDeathSubscribeRoutine = null;
        }

        if (_subscribedLocalEntity != null)
        {
            _subscribedLocalEntity.Died -= HandleLocalPlayerDied;
            _subscribedLocalEntity = null;
        }
    }

    private IEnumerator SubscribeToLocalDeathWhenReady()
    {
        while (LocalPlayer.Controller == null)
        {
            yield return null;
        }

        _localPlayerDead = !LocalPlayer.Controller.IsAlive;
        SetInputInteractable(!_localPlayerDead);

        _subscribedLocalEntity = LocalPlayer.Controller.Entity;
        _subscribedLocalEntity.Died += HandleLocalPlayerDied;

        _localDeathSubscribeRoutine = null;
    }

    // Ghosts don't get a say. Locks the input the instant this client's
    // player dies, even if that happens while the chat panel is already
    // open mid-Voting-phase.
    private void HandleLocalPlayerDied()
    {
        _localPlayerDead = true;
        SetInputInteractable(false);
    }

    private void SetInputInteractable(bool interactable)
    {
        if (chatInputField != null) chatInputField.interactable = interactable;
    }

    public void SendChatMessage()
    {
        if (_localPlayerDead)
        {
            AddLocalChatMessage("System: Ghosts can't chat.");
            return;
        }

        if (!IsChatAllowed)
        {
            AddLocalChatMessage("System: Chat is only available during the Voting Phase.");
            return;
        }

        if (chatInputField == null) return;

        string message = chatInputField.text.Trim();
        if (string.IsNullOrEmpty(message)) return;

        chatInputField.text = "";

        string senderName = PlayerDisplayName.For(LocalPlayer.Controller);
        FixedString128Bytes fixedMessage = senderName + ": " + message;

        SendChatMessageRpc(fixedMessage);
    }

    // rpcParams defaults to the sending client, so the sender's identity
    // comes from Netcode itself rather than a client-supplied argument —
    // matches the server-authoritative pattern SubmitVoteRpc already uses.
    // The client-side _localPlayerDead check above only stops a
    // well-behaved client from sending; a modified client could still fire
    // this RPC directly, so the server has to be the real gate.
    [Rpc(SendTo.Server)]
    private void SendChatMessageRpc(FixedString128Bytes message, RpcParams rpcParams = default)
    {
        // Server re-checks the phase guard; never trust the client's local flag.
        if (!IsChatAllowed) return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        NetworkPlayerController sender = PhaseContext.GetAllPlayersStatic()
            .FirstOrDefault(player => player.ClientId == senderId);
        if (sender == null || !sender.IsAlive)
        {
            return;
        }

        BroadcastChatMessageRpc(message);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void BroadcastChatMessageRpc(FixedString128Bytes message)
    {
        AddLocalChatMessage(message.ToString());
    }

    private void AddLocalChatMessage(string message)
    {
        if (chatDisplayText == null) return;
        chatDisplayText.text += "\n" + message;
    }
}
