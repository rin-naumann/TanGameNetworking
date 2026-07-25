// TEMPORARY, TEST-ONLY — delete once a real phase HUD exists.
using UnityEngine;

public class PhaseManagerTester : MonoBehaviour
{
    [SerializeField] private KeyCode logStatusKey = KeyCode.P;
    [SerializeField] private KeyCode forceGameOverKey = KeyCode.G;

    private void Update()
    {
        if (PhaseManager.Instance == null) return;

        if (Input.GetKeyDown(logStatusKey))
        {
            Debug.Log($"[PhaseManagerTester] Phase={PhaseManager.Instance.CurrentPhase}, " +
                      $"TimeRemaining={PhaseManager.Instance.PhaseTimeRemaining:F1}");
        }

        // GameOverPhase has no natural trigger yet — that's Day 2 kill/vote
        // logic (a killer-eliminated vote, or only 2 players remaining).
        // This is the only way to reach/test it before that exists.
        if (Input.GetKeyDown(forceGameOverKey))
        {
            PhaseManager.Instance.TriggerGameOver("Manually triggered by PhaseManagerTester.");
        }
    }

    private void Start()
    {
        if (PhaseManager.Instance != null)
        {
            PhaseManager.Instance.PhaseChanged += p => Debug.Log($"[PhaseManagerTester] Phase changed to {p}");
        }
    }
}