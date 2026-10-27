using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Draws a centered footer status between two fixed action buttons.</summary>
    public static void FooterStatus(
        this UIBuilder<Material> ui,
        IntRect footer,
        IntRect leftButton,
        IntRect rightButton,
        string? message,
        Vector4 color
    )
    {
        int left = leftButton.Right + 12;
        int right = rightButton.Left - 12;
        int width = System.Math.Max(0, right - left);
        if (string.IsNullOrEmpty(message) || width <= 0)
        {
            return;
        }

        ui.Label(
            message,
            UiMetrics.MENU_SMALL_FONT,
            color,
            RectConstraints(Rect(left, footer.Top + 6, width, footer.Size.Y - 12)),
            HorizontalAlignment.Center
        );
    }

    /// <summary>Draws a centered footer status in the current horizontal footer row.</summary>
    public static void FooterStatus(
        this UIBuilder<Material> ui,
        string? message,
        Vector4 color
    )
    {
        if (string.IsNullOrEmpty(message))
        {
            ui.Filler();
            return;
        }

        ui.Label(
            message,
            UiMetrics.MENU_SMALL_FONT,
            color,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            },
            HorizontalAlignment.Center
        );
    }
}
