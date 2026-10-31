using System.Collections.Generic;
using System.Numerics;
using Reef.Constraints;
using Reef.UI;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Display data for one progress bar notification.</summary>
internal readonly record struct NotificationBarState(
    string Text,
    string Subtext,
    bool HasSubtext,
    float Amount,
    IReadOnlyList<int> Dividers,
    float Alpha,
    Vector4? FillColor
);

/// <summary>Display data for the progress bar notification stack.</summary>
internal readonly record struct BarsState(IReadOnlyList<NotificationBarState> Bars);

/// <summary>Draws progress bar notifications with title, subtext, fill, and segments.</summary>
internal partial class BarsView
{
    /// <summary>Returns the faded panel background.</summary>
    protected static Vector4 PanelColor(float alpha) => DynamicColors.Fade(DynamicColors.PanelBackground, alpha);

    /// <summary>Returns the faded border color.</summary>
    protected static Vector4 BorderColor(float alpha) => DynamicColors.Fade(DynamicColors.Border, alpha);

    /// <summary>Returns the faded track background.</summary>
    protected static Vector4 TrackColor(float alpha) => DynamicColors.Fade(DynamicColors.Track, alpha);

    /// <summary>Returns the faded title and subtext color.</summary>
    protected static Vector4 TextColor(float alpha) => DynamicColors.Fade(DynamicColors.Text, alpha);

    /// <summary>Returns the faded fill color, using the accent when the payload has none.</summary>
    protected static Vector4 FillColor(float alpha, Vector4? color)
    {
        return DynamicColors.Fade(color ?? DynamicColors.Accent, alpha);
    }

    /// <summary>Returns the fill width constraint for a normalized amount.</summary>
    protected static IConstraint FillWidth(float amount) => new Relative(System.Math.Clamp(amount, 0f, 1f));
}
