using System.Collections.Generic;
using System.Numerics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Display data for the action strip.</summary>
internal readonly record struct ActionsState(
    string ActionText,
    float ActionAlpha,
    bool HasAction,
    IReadOnlyList<IndicatorState> Indicators
);

/// <summary>Draws the action notification and indicator icons.</summary>
internal partial class ActionsView
{
    /// <summary>Returns the faded action text color.</summary>
    protected static Vector4 TextColor(float alpha)
    {
        return DynamicColors.Fade(DynamicColors.Text, alpha);
    }
}
