using System.Collections;
using UnityEngine;

// Central coordinator for TOP-LEVEL panel visibility only (chat, HUD).
// Individual scripts still own their own panel's CONTENT (LobbyReadyManager
// fills ready counts, VotingUI rebuilds rows, GameOverUI sets reason text) -
// this just owns WHEN each panel is shown/hidden, so that isn't duplicated
// or forgotten across every script that happens to care about game state.
// MAIN-MENU/LOBBY visibility stays owned by ConnectionManager/
// LobbyReadyManager respectively since those already work correctly and
// are tied to connection callbacks, not phase changes.
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Top-level panels")]
    [SerializeField] private GameObject chatPanel;
    [SerializeField] private GameObject playerHudPanel;

    private void Awake()
    {
        Instance = this;
        SetChatVisible(false);
        SetHudVisible(false);
    }

    private void OnEnable()
    {
        StartCoroutine(SubscribeWhenReady());
    }

    private void OnDisable()
    {
        if (PhaseManager.Instance != null)
        {
            PhaseManager.Instance.PhaseChanged -= HandlePhaseChanged;
        }
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (PhaseManager.Instance == null) yield return null;
        PhaseManager.Instance.PhaseChanged += HandlePhaseChanged;
    }

    private void HandlePhaseChanged(PhaseManager.GamePhase phase)
    {
        // Chat only visible during Voting, per design doc - Roaming and
        // GameOver both hide it (LobbyMenuChat.IsChatAllowed already blocks
        // SENDING outside Voting; this hides the panel itself too).
        SetChatVisible(phase == PhaseManager.GamePhase.Voting);
    }

    // Called by LobbyReadyManager.BeginMatch() once the match actually starts.
    public void ShowMatchUI()
    {
        SetHudVisible(true);
    }

    private void SetChatVisible(bool visible)
    {
        if (chatPanel != null) chatPanel.SetActive(visible);
    }

    private void SetHudVisible(bool visible)
    {
        if (playerHudPanel != null) playerHudPanel.SetActive(visible);
    }
}
