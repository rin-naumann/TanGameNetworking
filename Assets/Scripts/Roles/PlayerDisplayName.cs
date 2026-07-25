// Central place to turn a player into the text UI should show for them.
// Always resolves off CurrentMaskId, never off the hidden Role NetworkVariable
// -- CurrentMaskId is already public/Everyone-synced (see MaskVisual, which
// colors the mask mesh off the same field), so this can never leak secret
// role info: for every non-killer player Mask == Role already, and for the
// killer it naturally resolves to whatever mask they're currently wearing
// (their last victim's role, or Visitor before their first kill).
//
// NOTE: because the killer and one random other player both start wearing
// the Visitor mask (by design, for round-one cover), two different players
// can legitimately show the same display name at the same time. That's the
// disguise working as intended, not a bug here -- callers that need to act
// on a specific player (vote buttons, etc.) should keep keying off ClientId
// and only use this for the label text.
public static class PlayerDisplayName
{
    public static string For(NetworkPlayerController player)
    {
        return player != null ? NameForRole(player.CurrentMaskId) : "Unknown";
    }

    public static string For(ulong clientId)
    {
        foreach (NetworkPlayerController player in PhaseContext.GetAllPlayersStatic())
        {
            if (player.ClientId == clientId) return NameForRole(player.CurrentMaskId);
        }
        return "Unknown";
    }

    // Same as For(...), but wrapped in a TMP rich-text <color> tag matching
    // the role's mask color (RoleColor) -- use this for any TMP_Text that
    // displays a role name, so the name always reads in the same hue as the
    // player's mask (VotingUI's row labels, StalkerRole's role-event pushes,
    // etc). Falls back to plain "Unknown" (no tag) when there's no player to
    // resolve a color from.
    public static string ForColored(NetworkPlayerController player)
    {
        return player != null ? ColoredNameForRole(player.CurrentMaskId) : "Unknown";
    }

    public static string ForColored(ulong clientId)
    {
        foreach (NetworkPlayerController player in PhaseContext.GetAllPlayersStatic())
        {
            if (player.ClientId == clientId) return ColoredNameForRole(player.CurrentMaskId);
        }
        return "Unknown";
    }

    private static string ColoredNameForRole(PlayerRole role)
    {
        return $"<color={RoleColor.HexForRole(role)}>{NameForRole(role)}</color>";
    }

    private static string NameForRole(PlayerRole role) => role switch
    {
        PlayerRole.Visitor => "Visitor",
        PlayerRole.Detective => "Detective",
        PlayerRole.Security => "Security",
        PlayerRole.Stalker => "Stalker",
        PlayerRole.Surveillance => "Surveillance",
        PlayerRole.Bookkeeper => "Bookkeeper",
        PlayerRole.Neighbor => "Neighbor",
        _ => role.ToString(),
    };
}
