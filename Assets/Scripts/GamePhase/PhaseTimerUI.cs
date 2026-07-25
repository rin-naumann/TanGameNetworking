using UnityEngine;
using TMPro;

// FIX: originally subscribed once in OnEnable, guarded by
// "if (PhaseManager.Instance != null)" — same race as GameOverUI (see that
// file for the full explanation). If PhaseManager.Awake() hadn't run yet,
// the phase LABEL subscription was silently skipped forever (the numeric
// timerText still worked since Update() polls PhaseTimeRemaining directly
// regardless of subscription state). Now retries each frame until
// PhaseManager.Instance exists, then subscribes once and syncs immediately.
public class PhaseTimerUI : MonoBehaviour
{
    [SerializeField] private TMP_Text phaseLabelText;
    [SerializeField] private TMP_Text timerText;

    private bool _subscribed;

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (_subscribed && PhaseManager.Instance != null)
            PhaseManager.Instance.PhaseChanged -= HandlePhaseChanged;
        _subscribed = false;
    }

    private void TrySubscribe()
    {
        if (_subscribed || PhaseManager.Instance == null) return;

        PhaseManager.Instance.PhaseChanged += HandlePhaseChanged;
        _subscribed = true;
        HandlePhaseChanged(PhaseManager.Instance.CurrentPhase);
    }

    // Countdown is a live NetworkVariable, not an event — poll it in Update
    // rather than trying to event-ify something that ticks every frame.
    private void Update()
    {
        if (!_subscribed) TrySubscribe();

        if (PhaseManager.Instance == null || timerText == null) return;
        timerText.text = Mathf.CeilToInt(PhaseManager.Instance.PhaseTimeRemaining).ToString();
    }

    private void HandlePhaseChanged(PhaseManager.GamePhase phase)
    {
        if (phaseLabelText == null) return;

        phaseLabelText.text = phase switch
        {
            PhaseManager.GamePhase.Roaming => "Roaming Phase",
            PhaseManager.GamePhase.Voting  => "Voting Phase",
            PhaseManager.GamePhase.GameOver => "Game Over",
            _ => ""
        };
    }
}
