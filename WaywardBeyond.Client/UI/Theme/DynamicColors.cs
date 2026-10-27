using System;
using System.Numerics;

namespace WaywardBeyond.Client.UI.Theme;

/// <summary>Fades and rich-text tags for the theme tokens that change at run time.</summary>
internal static class DynamicColors
{
    /// <summary>The accent color as a rich-text color tag.</summary>
    public static readonly string AccentRichText = ToRichText(WbPalette.Accent);

    /// <summary>The bright accent color as a rich-text color tag.</summary>
    public static readonly string AccentBrightRichText = ToRichText(WbPalette.AccentBright);

    /// <summary>Multiplies the alpha channel of a color by a fade amount.</summary>
    public static Vector4 Fade(in Vector4 color, float alpha)
    {
        return new Vector4(color.X, color.Y, color.Z, color.W * alpha);
    }

    /// <summary>Formats a color as a rich-text color tag.</summary>
    public static string ToRichText(in Vector4 color)
    {
        return $"#{(byte)MathF.Round(color.X * 255f):X2}{(byte)MathF.Round(color.Y * 255f):X2}{(byte)MathF.Round(color.Z * 255f):X2}";
    }
}
