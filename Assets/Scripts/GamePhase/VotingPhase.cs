using System.Linq;

// Players discuss via chat and vote to eliminate one player. Lasts 1 minute
// per the design doc — Duration is constructor-injected so PhaseManager can
// override it for testing without this class hardcoding a test-only value.
// A tie results in no elimination.
//
// REFACTOR NOTE: vote tallying and the "did we just eliminate the Killer"
// win-condition check used to live in PhaseManager.AdvancePhase(), pulled
// out specifically to dodge a re-entrancy bug (calling TriggerGameOver from
// inside this phase's own Exit() would re-enter PhaseManager's transition
// logic mid-call). That's fixed structurally now instead: GetNextPhase()
// runs BEFORE Exit(), from inside PhaseManager.ChangeState, so the outcome
// is fully decided before Exit() ever executes, and Exit() is guaranteed
// not to re-trigger a transition. The tally logic now lives here, where it
// belongs.
public class VotingPhase : IGamePhase
{
    public PhaseManager.GamePhase Id => PhaseManager.GamePhase.Voting;
    public float Duration { get; }

    public VotingPhase(float duration = 60f)
    {
        Duration = duration;
    }

    public void Enter(PhaseContext ctx)
    {
        // Chat gate (LobbyMenuChat.IsChatAllowed) is flipped by
        // PhaseManager's NetworkVariable callback, not here — that has to
        // run identically on every client, and this Enter only ever runs
        // on the server.
        foreach (NetworkPlayerController player in ctx.GetAllPlayers())
        {
            player.SetMovementEnabled(false);
        }

        VotingManager.Instance?.ResetVotes();
    }

    public void Tick(PhaseContext ctx, float deltaTime, float timeRemaining) { }

    public void Exit(PhaseContext ctx) { }

    // Resolves the vote, applies the elimination (if any), and decides
    // whether that elimination ends the game. Matches the tie-means-
    // no-elimination rule from the design doc.
    public PhaseManager.GamePhase GetNextPhase(PhaseContext ctx)
    {
        ulong? eliminatedId = VotingManager.Instance?.TallyVotes();
        NetworkPlayerController eliminated = eliminatedId.HasValue
            ? ctx.FindPlayerByClientId(eliminatedId.Value)
            : null;

        if (eliminated == null)
        {
            // Covers both an actual tie and "nobody voted."
            return PhaseManager.GamePhase.Roaming;
        }

        eliminated.Entity.MarkDead();
        GameEventManager.RaisePlayerVotedOut(eliminatedId.Value);

        if (eliminated.IsKillerRole)
        {
            ctx.Manager.SetGameOverReason("Players voted out the Killer.");
            return PhaseManager.GamePhase.GameOver;
        }

        // Wrong player eliminated. Same rule NetworkPlayerAttack.CheckWinCondition
        // applies after a kill also applies here: the Killer wins at 2 players
        // remaining, regardless of HOW the manor got down to 2 (a kill or a bad
        // vote). Checked AFTER MarkDead() above, so it reflects the elimination
        // that just happened.
        int aliveCount = ctx.GetAllPlayers().Count(player => player.IsAlive);
        if (aliveCount <= 2)
        {
            ctx.Manager.SetGameOverReason("The Killer reduced the manor to 2 survivors.");
            return PhaseManager.GamePhase.GameOver;
        }

        return PhaseManager.GamePhase.Roaming;
    }
}
