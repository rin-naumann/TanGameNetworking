using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

// Server-authoritative vote collection for the Voting Phase. A separate
// NetworkBehaviour (not folded into VotingPhase, which is a plain C# class
// with no RPC capability of its own) because RPCs need to live on a
// NetworkBehaviour instance. Same in-scene-singleton placement pattern as
// RoomsManager/PhaseManager.
public class VotingManager : NetworkBehaviour
{
    public static VotingManager Instance { get; private set; }

    // Sentinel target id for a skip/abstain vote — deliberately not a
    // value any real clientId could ever collide with (Netcode assigns
    // small sequential clientIds). A skip vote that wins the tally still
    // resolves to "no elimination": TallyVotes() returns this id like any
    // other, but PhaseManager.AdvancePhase's FindPlayerByClientId won't
    // find a matching player for it, which is exactly the no-elimination
    // outcome skip is supposed to produce.
    public const ulong SkipVoteId = ulong.MaxValue;

    // voterId -> targetId. Server-only, cleared at the start of every
    // Voting Phase by VotingPhase.OnEnter.
    private readonly Dictionary<ulong, ulong> _votes = new Dictionary<ulong, ulong>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Server-only. Called by VotingPhase.OnEnter.
    public void ResetVotes()
    {
        if (!IsServer) return;
        _votes.Clear();
    }

    // Convenience wrapper for UI Buttons — Unity's Button.onClick only
    // supports int/float/string/bool params, not ulong, so a vote-for-this-
    // player button can call this directly instead of needing a ulong
    // somewhere in the Inspector.
    public void SubmitVote(int targetClientId) => SubmitVoteRpc((ulong)targetClientId);

    // Skip/abstain — separate from SubmitVote(int) since SkipVoteId can't
    // round-trip through an int the way a real small clientId can.
    public void SubmitSkipVote() => SubmitVoteRpc(SkipVoteId);

    // rpcParams defaults to the sending client, so the voter's identity
    // comes from Netcode itself rather than being trusted from a
    // client-supplied argument — matches the server-authoritative pattern
    // used everywhere else in this project.
    //
    // Both the "no self-votes" and "no re-votes" rules are enforced here,
    // not just in VotingUI — the UI only disables buttons for a well-behaved
    // client. A modified client could still fire this RPC directly, so the
    // server has to be the actual source of truth for both rules.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SubmitVoteRpc(ulong targetId, RpcParams rpcParams = default)
    {
        if (PhaseManager.Instance == null || PhaseManager.Instance.CurrentPhase != PhaseManager.GamePhase.Voting)
        {
            return;
        }

        ulong voterId = rpcParams.Receive.SenderClientId;

        // Dead players don't get a say — without this, a corpse could still
        // cast (or change) a vote for the rest of the phase, since nothing
        // else in the RPC path checks the sender's alive state.
        NetworkPlayerController voter = PhaseContext.GetAllPlayersStatic()
            .FirstOrDefault(player => player.ClientId == voterId);
        if (voter == null || !voter.IsAlive)
        {
            return;
        }

        // Can't vote for yourself. SkipVoteId (ulong.MaxValue) can never
        // collide with a real voterId, so skip votes are unaffected.
        if (targetId == voterId)
        {
            return;
        }

        // One vote per player per phase. _votes is cleared at the start of
        // each Voting Phase (see ResetVotes), so this only blocks a second
        // vote WITHIN the current phase, not across phases.
        if (_votes.ContainsKey(voterId))
        {
            return;
        }

        _votes[voterId] = targetId;
        Debug.Log($"[VotingManager] Client {voterId} voted for {targetId}.");
    }

    // Server-only. Called by PhaseManager.AdvancePhase() — see the note
    // there on why the tally happens there and not in VotingPhase.OnExit.
    // Returns the eliminated clientId, or null on a tie (including "nobody
    // voted"), matching the design doc's tie-means-no-elimination rule.
    public ulong? TallyVotes()
    {
        if (_votes.Count == 0) return null;

        Dictionary<ulong, int> counts = new Dictionary<ulong, int>();
        foreach (ulong targetId in _votes.Values)
        {
            counts.TryGetValue(targetId, out int count);
            counts[targetId] = count + 1;
        }

        ulong topTarget = 0;
        int topCount = 0;
        bool tie = false;

        foreach (KeyValuePair<ulong, int> entry in counts)
        {
            if (entry.Value > topCount)
            {
                topCount = entry.Value;
                topTarget = entry.Key;
                tie = false;
            }
            else if (entry.Value == topCount)
            {
                tie = true;
            }
        }

        return tie ? (ulong?)null : topTarget;
    }
}
