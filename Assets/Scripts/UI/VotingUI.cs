using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VotingUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject votingPanel;
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private GameObject playerVoteRowPrefab;
    [SerializeField] private Button skipButton;

    private readonly List<GameObject> _spawnedRows = new List<GameObject>();

    // Tracks whether the local player has locked in a vote/skip THIS phase.
    // Reset back to false every time we enter Voting again (HandlePhaseChanged).
    // This is a client-side UX lock only — VotingManager.SubmitVoteRpc is the
    // real (server-authoritative) enforcement, since a modified client could
    // otherwise just call the RPC directly and bypass this.
    private bool _hasVoted;

    // PhaseManager.Instance is assigned in PhaseManager.Awake(), which runs
    // on a networked object spawned when the host starts. That can happen
    // AFTER this component's OnEnable (this panel/canvas is active from
    // scene load), so we can't just check Instance != null once here — if
    // it's null at that moment we'd silently never subscribe and the panel
    // would never show for the rest of the session. Poll until it exists.
    private PhaseManager _subscribedManager;
    private Coroutine _subscribeRoutine;

    // Same timing problem as PhaseManager.Instance above, but for the local
    // player: LocalPlayer.Controller is set in NetworkPlayerController's own
    // OnNetworkSpawn (via LocalPlayer.Register), which can also run after
    // this component's OnEnable. We need it wired up so we can (a) gate the
    // panel on whether the LOCAL player is alive, not just the phase, and
    // (b) close the panel immediately if the local player dies while voting
    // is already open.
    private NetworkPlayerEntity _subscribedLocalEntity;
    private Coroutine _localDeathSubscribeRoutine;

    private void OnEnable()
    {
        if (skipButton != null)
        {
            skipButton.onClick.AddListener(HandleSkipClicked);
        }

        SetPanelVisible(false);

        _subscribeRoutine = StartCoroutine(SubscribeWhenReady());
        _localDeathSubscribeRoutine = StartCoroutine(SubscribeToLocalDeathWhenReady());
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (PhaseManager.Instance == null)
        {
            yield return null;
        }

        _subscribedManager = PhaseManager.Instance;
        _subscribedManager.PhaseChanged += HandlePhaseChanged;

        // We may have missed the PhaseChanged event entirely if the phase
        // already flipped to Voting before we finished subscribing above.
        // Sync to whatever the current phase actually is right now.
        HandlePhaseChanged(_subscribedManager.CurrentPhase);

        _subscribeRoutine = null;
    }

    private IEnumerator SubscribeToLocalDeathWhenReady()
    {
        while (LocalPlayer.Controller == null)
        {
            yield return null;
        }

        _subscribedLocalEntity = LocalPlayer.Controller.Entity;
        _subscribedLocalEntity.Died += HandleLocalPlayerDied;

        _localDeathSubscribeRoutine = null;
    }

    // Dead players don't get a say. If the local player dies while the
    // voting panel happens to already be open, close it immediately rather
    // than waiting for the next phase change — the server would silently
    // reject any vote from a dead player anyway (VotingManager.SubmitVoteRpc),
    // but leaving a fully interactive panel up for them to click is exactly
    // the "dead players can still vote" experience this fixes.
    private void HandleLocalPlayerDied()
    {
        SetPanelVisible(false);
        ClearRows();
    }

    private void OnDisable()
    {
        if (_subscribeRoutine != null)
        {
            StopCoroutine(_subscribeRoutine);
            _subscribeRoutine = null;
        }

        if (_localDeathSubscribeRoutine != null)
        {
            StopCoroutine(_localDeathSubscribeRoutine);
            _localDeathSubscribeRoutine = null;
        }

        if (_subscribedManager != null)
        {
            _subscribedManager.PhaseChanged -= HandlePhaseChanged;
            _subscribedManager = null;
        }

        if (_subscribedLocalEntity != null)
        {
            _subscribedLocalEntity.Died -= HandleLocalPlayerDied;
            _subscribedLocalEntity = null;
        }

        if (skipButton != null)
        {
            skipButton.onClick.RemoveListener(HandleSkipClicked);
        }

        ClearRows();
    }

    private void HandlePhaseChanged(PhaseManager.GamePhase newPhase)
    {
        bool isVoting = newPhase == PhaseManager.GamePhase.Voting;

        // Gate on the LOCAL player's alive state, not just the phase. Without
        // this, a dead player still gets the fully interactive voting panel —
        // their vote would ultimately be dropped server-side, but they can
        // still click through the motions, which reads as "dead players can
        // vote" even though it never counts.
        bool localIsAlive = LocalPlayer.Controller != null && LocalPlayer.Controller.IsAlive;
        bool shouldShowPanel = isVoting && localIsAlive;

        SetPanelVisible(shouldShowPanel);

        if (shouldShowPanel)
        {
            _hasVoted = false;
            RebuildPlayerList();
        }
        else
        {
            ClearRows();
        }
    }

    private void SetPanelVisible(bool visible)
    {
        if (votingPanel != null) votingPanel.SetActive(visible);
    }

    private void RebuildPlayerList()
    {
        ClearRows();

        if (playerListContainer == null || playerVoteRowPrefab == null)
        {
            Debug.LogWarning("[VotingUI] playerListContainer or playerVoteRowPrefab not assigned in the Inspector.");
            return;
        }

        NetworkPlayerController[] players =
            FindObjectsByType<NetworkPlayerController>(FindObjectsSortMode.None);

        foreach (NetworkPlayerController player in players)
        {
            if (!player.IsAlive) continue; // dead players aren't votable

            GameObject row = Instantiate(playerVoteRowPrefab, playerListContainer);
            _spawnedRows.Add(row);

            bool isSelf = LocalPlayer.Controller != null && player == LocalPlayer.Controller;

            TMP_Text label = row.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = isSelf ? $"{PlayerDisplayName.ForColored(player)} (you)" : PlayerDisplayName.ForColored(player);
            }

            Button voteButton = row.GetComponentInChildren<Button>();
            if (voteButton != null)
            {
                if (isSelf)
                {
                    // You can't vote for yourself — show your own row so you
                    // can see yourself in the list, but leave the button inert
                    // rather than wiring a click handler for it.
                    voteButton.interactable = false;
                }
                else
                {
                    ulong targetId = player.ClientId; // captured per-row for the closure below
                    voteButton.onClick.AddListener(() => HandleVoteClicked(targetId));
                }
            }
        }

        // A rebuild can in principle happen after a vote was already locked
        // in (e.g. the alive-player roster changing mid-phase) — keep it locked.
        if (_hasVoted)
        {
            SetVotingInteractable(false);
        }
    }

    private void ClearRows()
    {
        foreach (GameObject row in _spawnedRows)
        {
            if (row != null) Destroy(row);
        }
        _spawnedRows.Clear();
    }

    private void HandleVoteClicked(ulong targetClientId)
    {
        if (VotingManager.Instance == null || _hasVoted) return;

        VotingManager.Instance.SubmitVote((int)targetClientId);
        _hasVoted = true;
        SetVotingInteractable(false);
        Debug.Log($"[VotingUI] Voted for client {targetClientId}.");
    }

    private void HandleSkipClicked()
    {
        if (VotingManager.Instance == null || _hasVoted) return;

        VotingManager.Instance.SubmitSkipVote();
        _hasVoted = true;
        SetVotingInteractable(false);
        Debug.Log("[VotingUI] Skipped vote.");
    }

    // Locks every vote-row button and the skip button after a vote is cast,
    // so a single click can't be followed by a second, different vote.
    private void SetVotingInteractable(bool interactable)
    {
        foreach (GameObject row in _spawnedRows)
        {
            if (row == null) continue;
            Button button = row.GetComponentInChildren<Button>();
            if (button != null) button.interactable = interactable;
        }

        if (skipButton != null) skipButton.interactable = interactable;
    }
}
