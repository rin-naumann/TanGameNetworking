// TEMPORARY, TEST-ONLY. Points RoleManager's pool at whichever specific
// roles you're trying to test this session, so a small local Multiplayer
// Play Mode group (main Editor + a few virtual players) doesn't have to
// depend on hitting 6+ real players before Bookkeeper/Neighbor even show
// up. Does nothing in a real Relay match - only wires the override at all
// while ConnectionManager's Developer Test Mode is on.
//
// Setup:
//   1. Drop this on any always-loaded GameObject in the same scene as
//      ConnectionManager (e.g. next to it), BEFORE entering Play Mode.
//   2. Turn on ConnectionManager's Developer Test Mode.
//   3. Set testRolePool below to the roles you want covered this session -
//      e.g. [Stalker, Bookkeeper, Neighbor] guarantees all three with just
//      4 total players (1 killer + exactly those 3 non-killer slots).
//   4. Host on the main Editor, join with 1-3 MPPM virtual players
//      (localhost, no Relay), ready up as normal.
//
// Delete this object (or its component) before any real playtest - leaving
// it in a scene that also has devTestMode off is harmless (the Awake guard
// below no-ops), but it should not ship.
using System.Collections.Generic;
using UnityEngine;

public class RoleTestOverrideSetter : MonoBehaviour
{
    [Tooltip("Non-killer roles are handed out in THIS order to the shuffled non-killer " +
             "player list. Put whatever you're testing this session here - e.g. " +
             "[Stalker, Bookkeeper, Neighbor] to cover exactly those three with a " +
             "4-player match (1 killer + 3 non-killer), instead of leaving it to " +
             "RolePool's fixed Detective/Security/Stalker default order.")]
    [SerializeField]
    private List<PlayerRole> testRolePool = new List<PlayerRole>
    {
        PlayerRole.Stalker,
        PlayerRole.Bookkeeper,
        PlayerRole.Neighbor,
    };

    private void Awake()
    {
        RoleManager.TestRolePoolOverride = testRolePool;
        Debug.Log($"[RoleTestOverrideSetter] Test role pool active: " +
                  string.Join(", ", testRolePool));
    }

    private void OnDestroy()
    {
        // Don't let a stale override leak into a session that doesn't have
        // this object present - e.g. leaving Play Mode, removing this
        // component, then re-entering Play Mode should fall back to
        // RoleManager's normal RolePool, not silently keep reusing this.
        if (RoleManager.TestRolePoolOverride == testRolePool)
        {
            RoleManager.TestRolePoolOverride = null;
        }
    }
}
