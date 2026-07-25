using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Drives the in-game (post-lobby) state machine: Roaming <-> Voting, with
// GameOver as a terminal state reachable from either. Deliberately scoped to
// ONLY what happens after the match starts — connection/ready-up stays
// LobbyReadyManager's problem entirely; this component does nothing at all
// until BeginMatch() is called.
//
// REFACTOR NOTE: this used to also contain the Voting tally and
// win-condition resolution directly inside AdvancePhase(), which meant a
// state machine where the manager — not the states — held phase-specific
// game logic. That's moved to each IGamePhase's GetNextPhase() now (see
// VotingPhase). PhaseManager's job is reduced to exactly three things: run
// the countdown, ask the current phase what's next, and be the one place
// that actually calls ChangeState. Forced transitions (TriggerGameOver,
// called directly by kill resolution) still bypass GetNextPhase entirely —
// they were never the source of the original re-entrancy bug; calling
// TriggerGameOver from *inside* a phase's own Exit() was, and that call
// site no longer exists.
//
// This must live on an in-scene NetworkObject (same placement pattern as
// RoomsManager) so it's already spawned server-side the moment the host
// starts, ready for LobbyReadyManager.BeginMatch() to call into it.
public class PhaseManager : NetworkBehaviour
{
    public static PhaseManager Instance { get; private set; }

    public enum GamePhase { Roaming, Voting, GameOver }

    [Header("Testing Overrides")]
    [Tooltip("TEST-ONLY. Shortens phases for fast iteration. Reset both to the " +
             "design doc's real values (180 / 60) before any real playtest — " +
             "nothing else in the codebase reads these except the constructors below.")]
    [SerializeField] private float roamingPhaseDurationOverride = 20f;
    [SerializeField] private float votingPhaseDurationOverride = 10f;

    // Server-authoritative, readable by everyone — clients need this for
    // HUD countdowns, the chat gate, and any future kill/vote UI gating.
    private readonly NetworkVariable<GamePhase> _currentPhase = new NetworkVariable<GamePhase>(
        GamePhase.Roaming, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public GamePhase CurrentPhase => _currentPhase.Value;

    private readonly NetworkVariable<float> _phaseTimeRemaining = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public float PhaseTimeRemaining => _phaseTimeRemaining.Value;

    // Set right before the GameOver transition — either by TriggerGameOver()
    // directly, or by a phase's GetNextPhase() calling SetGameOverReason()
    // first. Any results/HUD script can just read this once
    // CurrentPhase == GameOver.
    private readonly NetworkVariable<FixedString128Bytes> _gameOverReason = new NetworkVariable<FixedString128Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public string GameOverReason => _gameOverReason.Value.ToString();

    // Fires on every machine (server + all clients) whenever CurrentPhase
    // changes — local-only systems (HUD, kill/vote UI) hook in here instead
    // of trying to call into server-only IGamePhase methods.
    public event System.Action<GamePhase> PhaseChanged;

    private Dictionary<GamePhase, IGamePhase> _phases;
    private IGamePhase _current;
    private PhaseContext _context;
    private Coroutine _loopRoutine;

    // Bumped on every ChangeState call. Lets the tick loop below notice a
    // phase transitioned itself mid-tick (a kill dropping the alive count
    // to 2, triggering TriggerGameOver while Roaming's timer is still
    // running) and bail out immediately, instead of also applying its own
    // "timer ran out" advance on top of that.
    private int _phaseGeneration;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _phases = new Dictionary<GamePhase, IGamePhase>
        {
            { GamePhase.Roaming, new RoamingPhase(roamingPhaseDurationOverride) },
            { GamePhase.Voting, new VotingPhase(votingPhaseDurationOverride) },
            { GamePhase.GameOver, new GameOverPhase() },
        };
    }

    public override void OnNetworkSpawn()
    {
        _currentPhase.OnValueChanged += HandlePhaseChanged;
    }

    public override void OnNetworkDespawn()
    {
        _currentPhase.OnValueChanged -= HandlePhaseChanged;
    }

    // Runs on every machine — this is the one place local, non-server-only
    // systems react to a phase change.
    private void HandlePhaseChanged(GamePhase previous, GamePhase current)
    {
        LobbyMenuChat.IsChatAllowed = current == GamePhase.Voting;
        PhaseChanged?.Invoke(current);
    }

    // --- Server-only: match lifecycle ---

    // Called once by LobbyReadyManager.BeginMatch(), after players are
    // placed in the entrance room and movement is enabled. This is
    // deliberately the ONLY entry point into the state machine.
    public void BeginMatch()
    {
        if (!IsServer) return;
        if (_loopRoutine != null) return; // already running — don't double-start

        _context = new PhaseContext(this);
        _loopRoutine = StartCoroutine(RunPhaseLoop());
    }

    private IEnumerator RunPhaseLoop()
    {
        ChangeState(GamePhase.Roaming);

        while (CurrentPhase != GamePhase.GameOver)
        {
            int enteredGeneration = _phaseGeneration;
            _phaseTimeRemaining.Value = _current.Duration;

            while (_phaseGeneration == enteredGeneration && _phaseTimeRemaining.Value > 0f)
            {
                yield return null;
                _phaseTimeRemaining.Value = Mathf.Max(0f, _phaseTimeRemaining.Value - Time.deltaTime);
                _current.Tick(_context, Time.deltaTime, _phaseTimeRemaining.Value);
            }

            // Something already transitioned us away mid-tick (e.g. a kill
            // called TriggerGameOver) — don't also apply the natural
            // "timer expired" advance below on top of that.
            if (_phaseGeneration != enteredGeneration) continue;

            GamePhase next = _current.GetNextPhase(_context);
            ChangeState(next);
        }
    }

    private void ChangeState(GamePhase next)
    {
        _current?.Exit(_context);
        _phaseGeneration++;
        _currentPhase.Value = next;
        _current = _phases[next];
        _current.Enter(_context);
    }

    // Server-only. Sets the reason text without transitioning — used by a
    // phase's own GetNextPhase() when it decides the outcome is GameOver,
    // so the reason is in place before ChangeState's Enter(GameOver) runs.
    public void SetGameOverReason(string reason)
    {
        if (!IsServer) return;
        _gameOverReason.Value = reason;
    }

    // Server-only. The seam kill resolution calls the moment a win
    // condition is detected outside the normal phase-timer flow (only 2
    // players remain, mid-Roaming) — forces an immediate transition
    // regardless of the current phase's timer.
    //
    // Safe to call from anywhere EXCEPT from inside a phase's own Exit() —
    // nothing in this codebase does that anymore; GetNextPhase (called
    // before Exit, from ChangeState) is the correct seam for a phase to
    // decide "this outcome ends the game."
    public void TriggerGameOver(string reason)
    {
        if (!IsServer) return;
        if (CurrentPhase == GamePhase.GameOver) return;

        _gameOverReason.Value = reason;
        ChangeState(GamePhase.GameOver);
    }
}
