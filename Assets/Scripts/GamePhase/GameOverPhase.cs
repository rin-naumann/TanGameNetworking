// Terminal state reached when the Killer is voted out or 2 players remain; has no auto-timer.
public class GameOverPhase : IGamePhase
{
    public PhaseManager.GamePhase Id => PhaseManager.GamePhase.GameOver;
    public float Duration => 0f;

    public void Enter(PhaseContext ctx)
    {
        foreach (NetworkPlayerController player in ctx.GetAllPlayers())
        {
            player.SetMovementEnabled(false);
        }

        // GameOverReason is already set by whichever caller triggered this phase.
    }

    public void Tick(PhaseContext ctx, float deltaTime, float timeRemaining) { }

    public void Exit(PhaseContext ctx) { }

    // Never actually called; implemented only to satisfy the interface.
    public PhaseManager.GamePhase GetNextPhase(PhaseContext ctx) => PhaseManager.GamePhase.GameOver;
}