using UnityEngine;

// Single source of truth for "what color represents this role". Shared by
// MaskVisual (tints the 3D mask mesh) and any UI text that names a role, so
// the mesh color and the text color can never drift apart into two
// separate palettes as new roles get added in Tier 2/3.
public static class RoleColor
{
    public static Color ColorForRole(PlayerRole role) => role switch
    {
        PlayerRole.Visitor => new Color(0.2f, 0.2f, 0.2f), // was Color.white - unreadable on white UI buttons
        PlayerRole.Detective => new Color(0.2f, 0.4f, 1f),
        PlayerRole.Security => new Color(1f, 0.85f, 0.1f),
        PlayerRole.Stalker => new Color(0.5f, 0f, 0.5f),
        PlayerRole.Surveillance => new Color(0f, 0.8f, 0.8f),
        PlayerRole.Bookkeeper => new Color(0.55f, 0.3f, 0.1f),
        PlayerRole.Neighbor => new Color(0.1f, 0.8f, 0.2f),
        _ => Color.gray,
    };

    // Not a PlayerRole value (IsKiller is a separate bool on the entity),
    // so the killer's start-of-match label color lives here as its own
    // constant instead of a switch case.
    public static readonly Color KillerColor = new Color(0.8f, 0.05f, 0.05f);
    public static string KillerHex => "#" + ColorUtility.ToHtmlStringRGB(KillerColor);

    // TMP rich-text hex, e.g. "#3366FF", for embedding in a TMP_Text string
    // via <color=#3366FF>...</color>. TMP_Text has Rich Text enabled by
    // default, so callers can just wrap a role name in this and it renders
    // tinted with no extra component setup.
    public static string HexForRole(PlayerRole role) => "#" + ColorUtility.ToHtmlStringRGB(ColorForRole(role));
}
