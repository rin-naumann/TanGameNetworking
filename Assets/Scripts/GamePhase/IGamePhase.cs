// One state in the post-lobby state machine, owned and driven server-side by PhaseManager.
public interface IGamePhase
{
    PhaseManager.GamePhase Id { get; }

    // Seconds before PhaseManager auto-advances; <= 0 means no automatic timer.
    float Duration { get; }

    void Enter(PhaseContext ctx);

    // timeRemaining lets a phase react to thresholds without keeping its own countdown.
    void Tick(PhaseContext ctx, float deltaTime, float timeRemaining);

    void Exit(PhaseContext ctx);

    // Decides the next phase once this one's timer expires; called before Exit(), never used for forced transitions.
    PhaseManager.GamePhase GetNextPhase(PhaseContext ctx);
}