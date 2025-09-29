// Assets/Scripts/AppleTheme.cs
using UnityEngine;

/// <summary>
/// Paleta cromática unificada Apple-style y helper para mapping por porcentaje.
/// </summary>
public static class AppleTheme
{
    public static readonly Color DarkGreen  = new Color(0.00f, 0.55f, 0.25f, 1f);
    public static readonly Color LightGreen = new Color(0.40f, 0.70f, 0.30f, 1f);
    public static readonly Color Yellow     = new Color(1.00f, 0.80f, 0.20f, 1f);
    public static readonly Color Red        = new Color(0.90f, 0.20f, 0.20f, 1f);

    /// <summary>
    /// Devuelve un color por thresholds tipo Apple.
    /// ≥95 DarkGreen, ≥80 LightGreen, ≥70 Yellow, else Red.
    /// </summary>
    public static Color Status(float percent)
    {
        if (percent >= 95f) return DarkGreen;
        if (percent >= 80f) return LightGreen;
        if (percent >= 70f) return Yellow;
        return Red;
    }
}
