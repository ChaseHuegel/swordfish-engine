using Swordfish.Graphics;

namespace WaywardBeyond.Client.UI;

internal interface IActionIndicator
{
    bool IsVisible();

    /// <summary>Gets the icon drawn for this indicator.</summary>
    Material? IndicatorIcon { get; }
}