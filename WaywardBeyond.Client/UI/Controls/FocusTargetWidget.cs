using Reef;
using Reef.UI;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.UI.Controls;

/// <summary>Focuses a named element without drawing another element.</summary>
internal static class FocusTargetWidget
{
    /// <summary>Requests keyboard focus for the element with the given identifier.</summary>
    public static void FocusTarget(this UIBuilder<Material> ui, string target)
    {
        using (ui.Element())
        {
            ui.Passthrough = true;
            ui.Focus(target);
        }
    }
}
