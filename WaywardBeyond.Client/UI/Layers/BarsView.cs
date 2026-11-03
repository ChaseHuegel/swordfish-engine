using System.Collections.Generic;
using System.Numerics;
using Reef.UI;
using WaywardBeyond.Client.UI.Components;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Display data for one progress bar notification.</summary>
internal readonly record struct NotificationBarState(
    string Text,
    string Subtext,
    bool HasSubtext,
    float Amount,
    int SegmentCount,
    float Alpha,
    Vector4? FillColor
);

/// <summary>Display data for the progress bar notification stack.</summary>
internal readonly record struct BarsState(IReadOnlyList<NotificationBarState> Bars);

/// <summary>Draws progress bar notifications with title, subtext, fill, and segments.</summary>
internal partial class BarsView
{
    private const int INNER_WIDTH = 488;
    private const int SEGMENT_GAP = 2;
    private static readonly int[] NO_DIVIDERS = [];
    private readonly Dictionary<int, int[]> _dividerCache = [];

    /// <summary>Returns the divider positions for a segment count.</summary>
    protected IReadOnlyList<int> DividerPositions(int segmentCount)
    {
        if (segmentCount <= 1)
        {
            return NO_DIVIDERS;
        }

        if (_dividerCache.TryGetValue(segmentCount, out int[]? cached))
        {
            return cached;
        }

        var dividers = new int[segmentCount - 1];
        for (var segmentIndex = 1; segmentIndex < segmentCount; segmentIndex++)
        {
            dividers[segmentIndex - 1] = INNER_WIDTH * segmentIndex / segmentCount - SEGMENT_GAP / 2;
        }

        _dividerCache[segmentCount] = dividers;
        return dividers;
    }

    /// <summary>Returns the faded panel background.</summary>
    protected static Vector4 PanelColor(float alpha) => DynamicColors.Fade(WbPalette.Panel, alpha);

    /// <summary>Returns the faded border color.</summary>
    protected static Vector4 BorderColor(float alpha) => DynamicColors.Fade(WbPalette.Border, alpha);

    /// <summary>Returns the faded title and subtext color.</summary>
    protected static Vector4 TextColor(float alpha) => DynamicColors.Fade(WbPalette.Text, alpha);

    /// <summary>Returns the progress bar for one notification bar.</summary>
    protected static ProgressBarState Track(NotificationBarState bar)
    {
        return new ProgressBarState(bar.Amount, FillColor(bar.Alpha, bar.FillColor));
    }

    /// <summary>Returns the faded fill color, using the accent when the payload has none.</summary>
    protected static Vector4 FillColor(float alpha, Vector4? color)
    {
        return DynamicColors.Fade(color ?? WbPalette.Accent, alpha);
    }
}
