using Reef;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Draws a stat name with its first letter emphasized.</summary>
    public static void StatLabel(
        this UIBuilder<Material> ui,
        string label,
        IntRect rect,
        int fontSize,
        bool focused
    )
    {
        if (string.IsNullOrEmpty(label))
        {
            return;
        }

        string markup = $"#{(focused ? "orange" : "cyan")}{label[0]}#R{label[1..]}";
        ui.Label(
            markup,
            rect,
            fontSize,
            focused ? UiTheme.Text : UiTheme.TextMuted
        );
    }
}
