using System;
using System.Numerics;

namespace WaywardBeyond.Client.UI.Theme;

/// <summary>Semantic colors shared by all game UI.</summary>
internal static class UiTheme
{
    public const string FONT_UI = "Salmon Mono 9 Regular";
    public const string FONT_ICONS = "Font Awesome 6 Free Solid";
    public const string FONT_BRANDS = "Font Awesome 6 Free Brands";

    public static readonly Vector4 PanelBackground = FromRgb(17, 23, 35, 240);
    public static readonly Vector4 PanelSolid = FromRgb(17, 23, 35);
    public static readonly Vector4 PanelInner = FromRgb(13, 18, 28, 240);
    public static readonly Vector4 InputBackground = FromRgb(28, 36, 51);
    public static readonly Vector4 InputFocus = FromRgb(35, 46, 66);
    public static readonly Vector4 Border = FromRgb(43, 56, 78);
    public static readonly Vector4 BorderInner = FromRgb(26, 35, 51);
    public static readonly Vector4 BorderHover = FromRgb(73, 93, 122);
    public static readonly Vector4 Accent = FromRgb(75, 227, 227);
    public static readonly Vector4 AccentBright = FromRgb(165, 243, 252);
    public static readonly Vector4 AccentDark = FromRgb(22, 40, 56);
    public static readonly Vector4 Primary = FromRgb(242, 153, 74);
    public static readonly Vector4 PrimaryBright = FromRgb(255, 210, 125);
    public static readonly Vector4 PrimaryDark = FromRgb(61, 38, 20);
    public static readonly Vector4 Text = FromRgb(240, 243, 246);
    public static readonly Vector4 TextMuted = FromRgb(141, 160, 184);
    public static readonly Vector4 TextDim = FromRgb(69, 84, 107);
    public static readonly Vector4 TextSoft = FromRgb(191, 191, 191);
    public static readonly Vector4 Danger = FromRgb(224, 79, 122);
    public static readonly Vector4 DangerBright = FromRgb(255, 141, 166);
    public static readonly Vector4 DangerDark = FromRgb(61, 20, 30);
    public static readonly Vector4 Success = FromRgb(111, 207, 151);
    public static readonly Vector4 Warning = FromRgb(242, 204, 74);
    public static readonly Vector4 Track = FromRgb(28, 36, 51);
    public static readonly Vector4 TrackFill = FromRgb(75, 227, 227, 180);
    public static readonly Vector4 Thumb = FromRgb(240, 243, 246);
    public static readonly Vector4 ButtonBackground = FromRgb(18, 24, 38);
    public static readonly Vector4 ButtonHover = FromRgb(27, 37, 56);
    public static readonly Vector4 ControlBackground = FromRgb(21, 29, 42);
    public static readonly Vector4 SelectionBackground = FromRgb(23, 35, 54);
    public static readonly Vector4 RowSelected = FromRgb(0, 116, 255, 128);
    public static readonly Vector4 ScreenBackground = FromRgb(0, 0, 0);
    public static readonly Vector4 Scrim = FromRgb(0, 0, 0, 191);
    public static readonly Vector4 ScrimSoft = FromRgb(0, 0, 0, 128);
    public static readonly Vector4 ScrimHeavy = FromRgb(0, 0, 0, 242);
    public static readonly Vector4 Transparent = Vector4.Zero;

    /// <summary>The accent color as a rich-text color tag, for text that uses inline markup.</summary>
    public static readonly string AccentRichText = ToRichText(Accent);
    
    /// <summary>The accent color as a rich-text color tag, for text that uses inline markup.</summary>
    public static readonly string AccentBrightRichText = ToRichText(AccentBright);

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
