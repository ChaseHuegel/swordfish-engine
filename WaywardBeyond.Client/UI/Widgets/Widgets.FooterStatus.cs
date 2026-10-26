using System;
using System.Numerics;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Draws a centered footer status between two action buttons.</summary>
    public static void FooterStatus(
        this UIBuilder<Material> ui,
        IntRect footer,
        IntRect leftButton,
        IntRect rightButton,
        string? message,
        Vector4 color
    )
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        int left = leftButton.Right + 12;
        int right = rightButton.Left - 12;
        int width = Math.Max(0, right - left);
        if (width <= 0)
        {
            return;
        }

        ui.Label(
            message,
            Rect(left, footer.Top + 6, width, footer.Size.Y - 12),
            UiMetrics.MenuSmallFont,
            color,
            Anchors.Center
        );
    }
}
