using System;
using System.Numerics;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Resolves the icon, value text, and color for a ping reading.</summary>
    public static PingDisplay ResolvePing(int? ping)
    {
        if (ping == null)
        {
            return new PingDisplay(MenuGlyphs.Signal, "-", UiTheme.TextDim);
        }

        int value = ping.Value;
        if (value >= 250)
        {
            return new PingDisplay(MenuGlyphs.XMark, "-", UiTheme.Danger);
        }

        if (value < 60)
        {
            return new PingDisplay(MenuGlyphs.Signal, $"{value} ms", UiTheme.Success);
        }

        if (value < 100)
        {
            return new PingDisplay(MenuGlyphs.Signal, $"{value} ms", UiTheme.Warning);
        }

        if (value < 150)
        {
            return new PingDisplay(MenuGlyphs.Signal, $"{value} ms", UiTheme.Primary);
        }

        return new PingDisplay(MenuGlyphs.Signal, $"{value} ms", UiTheme.Danger);
    }

    /// <summary>Draws a ping icon and value in a fixed rectangle.</summary>
    public static void PingIndicator(this UIBuilder<Material> ui, IntRect rect, int? ping)
    {
        PingDisplay display = ResolvePing(ping);
        ui.Label(
            display.Glyph,
            Rect(rect.Left, rect.Top, 20, rect.Size.Y),
            UiMetrics.MenuSmallFont,
            display.Color,
            Anchors.Left | Anchors.Center,
            UiTheme.FontIcons
        );
        ui.Label(
            display.Text,
            Rect(rect.Left + 24, rect.Top, Math.Max(0, rect.Size.X - 24), rect.Size.Y),
            UiMetrics.MenuSmallFont,
            display.Color,
            Anchors.Left | Anchors.Center
        );
    }

    /// <summary>One resolved ping indicator state.</summary>
    public readonly record struct PingDisplay(string Glyph, string Text, Vector4 Color);
}
