// Players move freely, gather role information, and the killer may attempt
// one kill. Lasts 3 minutes per the design doc — Duration is
// constructor-injected so PhaseManager can override it for testing without
// this class hardcoding a test-only value.
//
// The exit mapping is deliberately re-rolled every round (not once at
// match start) — an intentional balance call, not the original design doc's
// literal "mapped once at game start" wording. A killer facing players who
// have fully memorized a static manor by round 2-3 has no way to predict
// where anyone is; re-rolling keeps movement disorienting for everyone
// every round, which is what actually keeps the killer viable late-game.
public class RoamingPhase : IGamePhase
{
    public PhaseManager.GamePhase Id => PhaseManager.GamePhase.Roaming;
    public float Duration { get; }

    public RoamingPhase(float duration = 180f)
    {
        Duration = duration;
    }

    public void Enter(PhaseContext ctx)
    {
        // Reroll where every door leads for the new round — same mapping for
        // every player. See class comment above for why this happens every
        // round rather than once at match start.
        ctx.Rooms?.GenerateExitMapping();

        foreach (NetworkPlayerController player in ctx.GetAllPlayers())
        {
            player.SetMovementEnabled(true);
            player.Entity.ResetRoundState();
        }

        // Raised AFTER the reset loop above, on purpose — Security's
        // protection pick runs synchronously off this event, and needs to
        // see everyone's IsProtected already cleared for the new round,
        // not last round's leftover state.
        GameEventManager.RaiseRoundStarted();
    }

    public void Tick(PhaseContext ctx, float deltaTime, float timeRemaining) { }

    public void Exit(PhaseContext ctx) { }

    // Roaming always naturally advances to Voting when its timer runs out —
    // it never ends the game itself. A kill dropping the alive count to 2
    // mid-Roaming ends the match via PhaseManager.TriggerGameOver() instead,
    // called directly from NetworkPlayerAttack, which bypasses this method
    // entirely (see IGamePhase.GetNextPhase for why forced transitions
    // don't go through here).
    public PhaseManager.GamePhase GetNextPhase(PhaseContext ctx) => PhaseManager.GamePhase.Voting;
}