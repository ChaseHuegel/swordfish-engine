using System;
using System.Numerics;

namespace WaywardBeyond.Client.UI.Theme;

/// <summary>Literal theme colors and fades for UI that changes alpha at run time.</summary>
internal static class DynamicColors
{
    public static readonly Vector4 Text = FromRgb(240, 243, 246);
    public static readonly Vector4 TextMuted = FromRgb(141, 160, 184);
    public static readonly Vector4 PanelBackground = FromRgb(17, 23, 35, 240);
    public static readonly Vector4 PanelInner = FromRgb(13, 18, 28, 240);
    public static readonly Vector4 Border = FromRgb(43, 56, 78);
    public static readonly Vector4 Track = FromRgb(28, 36, 51);
    public static readonly Vector4 Accent = FromRgb(75, 227, 227);
    public static readonly Vector4 AccentBright = FromRgb(165, 243, 252);
    public static readonly Vector4 ScrimSoft = FromRgb(0, 0, 0, 128);

    /// <summary>The accent color as a rich-text color tag.</summary>
    public static readonly string AccentRichText = ToRichText(Accent);

    /// <summary>The bright accent color as a rich-text color tag.</summary>
    public static readonly string AccentBrightRichText = ToRichText(AccentBright);

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

    private static Vector4 FromRgb(byte r, byte g, byte b, byte a = 255)
    {
        return new Vector4(r / 255f, g / 255f, b / 255f, a / 255f);
    }
}
