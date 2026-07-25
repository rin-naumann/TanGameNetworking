using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    public static PlayerHUD Instance { get; private set; }

    [Header("Notification Feed")]
    [SerializeField] private Transform feedContainer;
    [SerializeField] private GameObject feedEntryPrefab;
    [SerializeField] private int maxVisibleEntries = 8;

    [Header("Death Overlay")]
    [Tooltip("Full-screen panel shown once this player is eliminated. Starts inactive.")]
    [SerializeField] private GameObject deathOverlay;
    [SerializeField] private TMP_Text deathReasonText;
    [Tooltip("Lets an eliminated player leave the session instead of continuing to spectate.")]
    [SerializeField] private Button leaveSessionButton;

    [Header("Role Event Panel")]
    [Tooltip("Transient panel reporting what happened with this player's role ability (Detective body-found, Security protection, etc.).")]
    [SerializeField] private GameObject roleEventPanel;
    [SerializeField] private TMP_Text roleEventText;
    [SerializeField] private float roleEventDisplayDuration = 4f;

    [Header("Role Identifier")]
    [Tooltip("Persistent label showing this player's own role (or 'Killer'). Set once at match start and never auto-hidden - deliberately separate from the transient Role Event Panel above, which reports ability EVENTS, not identity.")]
    [SerializeField] private TMP_Text roleIdentifierText;

    private readonly List<GameObject> _entries = new List<GameObject>();
    private Coroutine _roleEventCoroutine;

    // Buffers HUD calls that arrive (via RPC) before this component's Awake
    // has run and set Instance — e.g. a role-ability RPC racing the local
    // ShowMatchUI() activation on a remote client. Without this, those calls
    // hit PlayerHUD.Instance?.Foo() while Instance is still null and silently
    // no-op instead of erroring — which is why the notification just never
    // appeared with nothing in the console.
    private static readonly List<System.Action<PlayerHUD>> _pendingCalls = new List<System.Action<PlayerHUD>>();

    public static void InvokeOrQueue(System.Action<PlayerHUD> action)
    {
        if (Instance != null)
        {
            action(Instance);
        }
        else
        {
            _pendingCalls.Add(action);
        }
    }

    private void Awake()
    {
        Instance = this;
        if (deathOverlay != null) deathOverlay.SetActive(false);
        if (roleEventPanel != null) roleEventPanel.SetActive(false);
        if (leaveSessionButton != null) leaveSessionButton.onClick.AddListener(HandleLeaveSessionClicked);

        // Flush anything that arrived before we existed.
        foreach (System.Action<PlayerHUD> pending in _pendingCalls)
        {
            pending(this);
        }
        _pendingCalls.Clear();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (leaveSessionButton != null) leaveSessionButton.onClick.RemoveListener(HandleLeaveSessionClicked);
    }

    public void AddNotification(string message)
    {
        if (feedContainer == null || feedEntryPrefab == null)
        {
            Debug.LogWarning("[PlayerHUD] feedContainer or feedEntryPrefab not assigned — dropping: " + message);
            return;
        }

        GameObject entry = Instantiate(feedEntryPrefab, feedContainer);
        TMP_Text label = entry.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = message;

        _entries.Add(entry);

        if (_entries.Count > maxVisibleEntries)
        {
            Destroy(_entries[0]);
            _entries.RemoveAt(0);
        }
    }

    public void ShowDeathOverlay(string reason)
    {
        if (deathOverlay != null) deathOverlay.SetActive(true);
        if (deathReasonText != null) deathReasonText.text = reason;

        AddNotification(reason);
    }

    // Sets the persistent role-identifier label ("Killer", "Detective", etc.)
    // shown for the rest of the match. Called exactly once, at role
    // assignment (see RoleManager.AssignRoles) - deliberately NOT routed
    // through ShowRoleEvent below, since that panel auto-hides after a few
    // seconds and mirrors into the scrolling notification feed, neither of
    // which is right for a label that's supposed to just sit there and
    // identify the player's role for the whole match.
    public void SetRoleIdentifier(string label)
    {
        if (roleIdentifierText != null) roleIdentifierText.text = label;
    }

    // Role-ability feedback (Detective body-found, Security protection, etc.) —
    // kept separate from the general notification feed so it reads as a
    // distinct callout rather than just another line in the scrolling log.
    // Still mirrored into the feed so there's a persistent record too.
    public void ShowRoleEvent(string message)
    {
        AddNotification(message);

        if (roleEventPanel == null || roleEventText == null) return;

        roleEventText.text = message;
        roleEventPanel.SetActive(true);

        if (_roleEventCoroutine != null) StopCoroutine(_roleEventCoroutine);
        _roleEventCoroutine = StartCoroutine(HideRoleEventAfterDelay());
    }

    private IEnumerator HideRoleEventAfterDelay()
    {
        yield return new WaitForSeconds(roleEventDisplayDuration);
        if (roleEventPanel != null) roleEventPanel.SetActive(false);
        _roleEventCoroutine = null;
    }

    // Lets an eliminated player disconnect and free up their seat instead of
    // being stuck spectating. NOTE: if this client is the host,
    // NetworkManager.Shutdown() ends the session for everyone — fine for a
    // single-scene test build, but worth a dedicated kick-self-only path
    // later if that distinction matters.
    private void HandleLeaveSessionClicked()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        if (deathOverlay != null) deathOverlay.SetActive(false);
    }
}
