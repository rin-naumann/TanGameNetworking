using TMPro;
using UnityEngine;

// Shows a full-screen results panel once PhaseManager reaches GameOver.
// Purely reactive to PhaseManager.PhaseChanged / GameOverReason — no
// server-side changes needed, per the design doc.
//
// FIX: originally subscribed once in OnEnable, guarded by
// "if (PhaseManager.Instance != null)". PhaseManager is a NetworkBehaviour,
// and its Awake() (which sets Instance) is not guaranteed to run before an
// ordinary MonoBehaviour's OnEnable() in the same scene load — when it
// didn't, the guard silently skipped the subscription forever and the
// panel never reacted to GameOver. Now retries each frame via Update()
// until PhaseManager.Instance exists, then subscribes once and immediately
// syncs to the current phase (covers the case where GameOver already
// happened before we managed to subscribe).
public class GameOverUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text reasonText;

    private bool _subscribed;

    private void OnEnable()
    {
        SetVisible(false);
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (_subscribed && PhaseManager.Instance != null)
        {
            PhaseManager.Instance.PhaseChanged -= HandlePhaseChanged;
        }
        _subscribed = false;
    }

    private void Update()
    {
        if (!_subscribed) TrySubscribe();
    }

    private void TrySubscribe()
    {
        if (_subscribed || PhaseManager.Instance == null) return;

        PhaseManager.Instance.PhaseChanged += HandlePhaseChanged;
        _subscribed = true;

        // Sync immediately in case the phase already changed before we
        // managed to subscribe.
        HandlePhaseChanged(PhaseManager.Instance.CurrentPhase);
    }

    private void HandlePhaseChanged(PhaseManager.GamePhase newPhase)
    {
        bool isGameOver = newPhase == PhaseManager.GamePhase.GameOver;
        SetVisible(isGameOver);

        if (isGameOver && reasonText != null && PhaseManager.Instance != null)
        {
            reasonText.text = PhaseManager.Instance.GameOverReason;
        }
    }

    private void SetVisible(bool visible)
    {
        if (panel != null) panel.SetActive(visible);
    }
}
