using System.Collections.Generic;
using UnityEngine;

// Server-only. Assigns exactly one Killer, then hands out roles from
// RolePool to the rest of the shuffled player list; anyone left over after
// the pool is exhausted defaults to Visitor.
//
// FIX (randomized role combos): BuildPool used to always drain RolePool in
// its fixed array order, so which ROLES showed up in a sub-7-player match
// was deterministic (always Detective/Security/Stalker for a 4-player
// game, never anything further down the list) — only WHO got each role was
// random. BuildPool now: reserves 1 guaranteed killer slot (handled by the
// caller) and 1 guaranteed Visitor slot, then fills the rest by shuffling
// RolePool itself and taking however many slots remain, so the same
// player count can land on a different mix of roles from one match to the
// next.
public static class RoleManager
{
    // Add an entry here for each new role as it's built. Order doesn't
    // matter — the pool is shuffled before being drained, so who gets what
    // AND which roles are even in play are both fully random.
    private static readonly (PlayerRole role, int count)[] RolePool =
    {
        (PlayerRole.Detective, 1),
        (PlayerRole.Security, 1),
        (PlayerRole.Stalker, 1),
        (PlayerRole.Bookkeeper, 1),
        (PlayerRole.Neighbor, 1),
        (PlayerRole.Surveillance, 1),
    };

    // Developer-only override for local testing with fewer than the normal
    // 4-8 players (e.g. Multiplayer Play Mode's per-project instance cap).
    // When set, BuildPool() drains THIS list instead of RolePool above, so
    // whichever 2-4 players you actually have on hand land on exactly the
    // roles you're trying to test. Without this, Bookkeeper/Neighbor are
    // unreachable in a 4-player test session — RolePool's fixed draining
    // order means the first 3 non-killer slots always go to
    // Detective/Security/Stalker, never further down the list.
    // Set only from a test-only script gated behind ConnectionManager's dev
    // test mode; never touched in a real match. Null/empty = use RolePool.
    public static List<PlayerRole> TestRolePoolOverride;

    public static void AssignRoles(List<NetworkPlayerController> controllers)
    {
        List<NetworkPlayerController> shuffled = Shuffle(controllers);
        Queue<PlayerRole> pool = BuildPool(shuffled.Count);

        for (int i = 0; i < shuffled.Count; i++)
        {
            NetworkPlayerController controller = shuffled[i];
            bool isKiller = i == 0;

            // Role value is unused/irrelevant for the killer — Visitor here
            // is just a harmless default, not a meaningful choice.
            PlayerRole role = isKiller
                ? PlayerRole.Visitor
                : (pool.Count > 0 ? pool.Dequeue() : PlayerRole.Visitor);

            PlayerRoleBehaviour behaviour = CreateBehaviour(role, isKiller);

            controller.AssignRole(role, isKiller);
            controller.Entity.SetCurrentRole(behaviour);
            behaviour.Subscribe(controller.Entity);

            // Tell every player what they got, killer and visitor included.
            // Uses its own dedicated delivery (PushRoleIdentifierRpc) and its
            // own persistent HUD label, kept deliberately separate from
            // PushRoleEventRpc/the Role Event Panel — that one is for
            // transient ability callouts (Detective body-found, etc.), not
            // for "what role am I" identity, which shouldn't auto-hide or
            // get mixed into the scrolling notification feed. Killer shows
            // "Killer" specifically rather than the Visitor mask they
            // start the match wearing.
            string roleLabel = isKiller
                ? $"<color={RoleColor.KillerHex}>Killer</color>"
                : PlayerDisplayName.ForColored(controller);
            controller.Entity.PushRoleIdentifierRpc(roleLabel);

            Debug.Log($"[RoleManager] Client {controller.OwnerClientId} -> " +
                      $"Role={controller.Role}, IsKiller={controller.IsKillerRole}");
        }
    }

    private static Queue<PlayerRole> BuildPool(int playerCount)
    {
        Queue<PlayerRole> pool = new Queue<PlayerRole>();

        // Test override takes priority whenever it's set — see the field's
        // comment above. Consumed fresh each match; the setter script is
        // responsible for clearing it when it's no longer wanted.
        if (TestRolePoolOverride != null && TestRolePoolOverride.Count > 0)
        {
            foreach (PlayerRole role in TestRolePoolOverride)
            {
                pool.Enqueue(role);
            }
            return pool;
        }

        // Index 0 of the shuffled list is always the killer (see
        // AssignRoles), so there are (playerCount - 1) non-killer slots to
        // fill. One of those is always reserved for a guaranteed Visitor;
        // the remainder are drawn randomly from RolePool.
        int nonKillerSlots = Mathf.Max(playerCount - 1, 0);
        int randomSlots = Mathf.Max(nonKillerSlots - 1, 0);

        List<PlayerRole> available = new List<PlayerRole>();
        foreach ((PlayerRole role, int count) in RolePool)
        {
            for (int i = 0; i < count; i++)
            {
                available.Add(role);
            }
        }
        ShuffleList(available);

        List<PlayerRole> chosen = available.Count > randomSlots
            ? available.GetRange(0, randomSlots)
            : new List<PlayerRole>(available);

        // More random slots than distinct roles exist (e.g. once an 8-player
        // match needs 6 random slots but RolePool hasn't grown to match) —
        // pad with extra Visitors rather than leaving slots unfilled.
        while (chosen.Count < randomSlots)
        {
            chosen.Add(PlayerRole.Visitor);
        }

        if (nonKillerSlots > 0)
        {
            chosen.Add(PlayerRole.Visitor); // the guaranteed one
        }

        ShuffleList(chosen);

        foreach (PlayerRole role in chosen)
        {
            pool.Enqueue(role);
        }
        return pool;
    }

    // Add a case here whenever a role's PlayerRoleBehaviour subclass is
    // built — this is the only other place that needs to know about a new
    // role besides RolePool itself.
    private static PlayerRoleBehaviour CreateBehaviour(PlayerRole role, bool isKiller) => role switch
    {
        PlayerRole.Detective => new DetectiveRole(),
        PlayerRole.Security => new SecurityRole(),
        PlayerRole.Stalker => new StalkerRole(),
        PlayerRole.Bookkeeper => new BookkeeperRole(),
        PlayerRole.Neighbor => new NeighborRole(),
        PlayerRole.Surveillance => new SurveillanceRole(),
        _ => new VisitorRole { IsKiller = isKiller },
    };

    private static List<NetworkPlayerController> Shuffle(List<NetworkPlayerController> source)
    {
        List<NetworkPlayerController> shuffled = new List<NetworkPlayerController>(source);
        ShuffleList(shuffled);
        return shuffled;
    }

    // Generic in-place Fisher-Yates shuffle, shared by both the player-order
    // shuffle above and the role-pool shuffle in BuildPool.
    private static void ShuffleList<T>(List<T> list)
    {
        System.Random rng = new System.Random();
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
