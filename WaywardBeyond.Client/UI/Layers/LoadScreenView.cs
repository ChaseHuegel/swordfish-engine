using System;
using System.Numerics;
using System.Text;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Display data for the loading screen.</summary>
internal readonly record struct LoadScreenState(
    string Status,
    string Hint,
    double CurrentTime,
    double StatusStartedAt,
    double HintStartedAt
);

/// <summary>Draws the full-screen loading state.</summary>
internal partial class LoadScreenView
{
    private const double FADE_IN_SECONDS = 0.3d;
    private const int SIGNAL_WIDTH = 160;
    private const int SIGNAL_SEGMENT_WIDTH = 48;
    private const int SIGNAL_TRAVEL = SIGNAL_WIDTH + SIGNAL_SEGMENT_WIDTH * 2;
    private const double SIGNAL_PERIOD_SECONDS = 2d;

    /// <summary>Gets the content fade at the current time.</summary>
    public static float ContentFade(double now) => Fade(now);

    /// <summary>Gets the status fade at the current time.</summary>
    public static float StatusFade(double now, double startedAt) => Fade(now - startedAt);

    /// <summary>Gets the hint fade at the current time.</summary>
    public static float HintFade(double now, double startedAt) => Fade(now - startedAt);

    /// <summary>Gets the signal segment position at the current time.</summary>
    public static int SignalX(double now) => (int)(now / SIGNAL_PERIOD_SECONDS % 1d * SIGNAL_TRAVEL) - SIGNAL_SEGMENT_WIDTH;

    private static float Fade(double elapsed) => (float)Math.Clamp(elapsed / FADE_IN_SECONDS, 0d, 1d);

    /// <summary>Returns the faded status text color.</summary>
    protected static Vector4 TextColor(float alpha)
    {
        return DynamicColors.Fade(WbPalette.Text, alpha);
    }

    /// <summary>Returns the faded hint text color.</summary>
    protected static Vector4 MutedColor(float alpha)
    {
        return DynamicColors.Fade(WbPalette.TextMuted, alpha);
    }

    /// <summary>Returns the faded signal line color.</summary>
    protected static Vector4 BorderColor(float alpha)
    {
        return DynamicColors.Fade(WbPalette.Border, alpha);
    }

    /// <summary>Returns the faded moving segment color.</summary>
    protected static Vector4 AccentColor(float alpha)
    {
        return DynamicColors.Fade(WbPalette.Accent, alpha);
    }

    /// <summary>Formats bracketed keys in a hint with rich-text color tags.</summary>
    protected static string FormatHint(string hint)
    {
        if (hint.IndexOf('[') < 0)
        {
            return hint;
        }

        var builder = new StringBuilder(hint.Length + 16);
        var inKey = false;
        for (var i = 0; i < hint.Length; i++)
        {
            char c = hint[i];
            if (c == '[' && !inKey)
            {
                builder.Append(DynamicColors.AccentRichText + " ");
                inKey = true;
            }

            builder.Append(c);
            if (c == ']' && inKey)
            {
                builder.Append("#R ");
                inKey = false;
            }
        }

        return builder.ToString();
    }
}
