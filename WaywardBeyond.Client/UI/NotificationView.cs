using System.Numerics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

/// <summary>Display data for one notification.</summary>
internal readonly record struct NotificationViewState(string Text, float Alpha, bool HasBackground);

/// <summary>Draws notification text with its lifetime fade.</summary>
internal partial class NotificationView
{
    /// <summary>Returns the faded background color.</summary>
    protected static Vector4 PanelColor(float alpha)
    {
        return DynamicColors.Fade(DynamicColors.ScrimSoft * DynamicColors.PanelInner, alpha);
    }

    /// <summary>Returns the faded accent bar color.</summary>
    protected static Vector4 AccentColor(float alpha) => DynamicColors.Fade(DynamicColors.Accent, alpha);

    /// <summary>Returns the faded text color.</summary>
    protected static Vector4 TextColor(float alpha) => DynamicColors.Fade(DynamicColors.Text, alpha);
}
