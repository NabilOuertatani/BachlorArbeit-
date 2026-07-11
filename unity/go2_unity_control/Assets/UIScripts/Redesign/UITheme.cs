using UnityEngine;

/// <summary>
/// Central design tokens for the gesture-console redesign.
/// Keep every color/radius used by the new UI in here so the
/// editor builder and runtime components stay consistent.
/// </summary>
public static class UITheme
{
    // Accent
    public static readonly Color Accent      = FromHex("1D9E75");
    public static readonly Color AccentSoft  = FromHex("E1F5EE");
    public static readonly Color Warning     = FromHex("BA7517");
    public static readonly Color WarningSoft = FromHex("FAEEDA");
    public static readonly Color Danger      = FromHex("E24B4A");

    // Surfaces
    public static readonly Color Page  = FromHex("1E2130");
    public static readonly Color Panel = FromHex("2A2E3E");
    public static readonly Color Card  = FromHex("3A3F52");

    // Text
    public static readonly Color TextPrimary   = FromHex("F1F2F6");
    public static readonly Color TextSecondary = FromHex("B9BECD");
    public static readonly Color TextMuted     = FromHex("7A8095");

    // Hairline borders
    public static readonly Color Hairline = new Color(1f, 1f, 1f, 0.10f);

    // Radii (canvas units)
    public const float RadiusCard    = 12f;
    public const float RadiusControl = 6f;

    public static Color FromHex(string hex)
    {
        return ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : Color.magenta;
    }

    /// <summary>Color with overridden alpha.</summary>
    public static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }
}
