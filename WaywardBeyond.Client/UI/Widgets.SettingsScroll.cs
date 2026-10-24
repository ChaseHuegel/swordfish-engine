using System;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Settings;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Draws a non-interactive scrollbar for a settings list.</summary>
    public static void SettingsScrollbar(
        this UIBuilder<Material> ui,
        IntRect trackRect,
        int contentHeight,
        int viewportHeight,
        int scrollY
    )
    {
        int maxScroll = Math.Max(0, contentHeight - viewportHeight);
        if (maxScroll == 0 || contentHeight <= 0 || viewportHeight <= 0)
        {
            return;
        }

        using (ui.Element())
        {
            ui.Color = SettingsTheme.Track;
            ui.Constraints = SettingsRectConstraints(trackRect);
            ui.ClipConstraints = SettingsRectConstraints(trackRect);
        }

        int thumbHeight = Math.Clamp(
            trackRect.Size.Y * viewportHeight / contentHeight,
            SettingsMetrics.ScrollbarThumbMinHeight,
            trackRect.Size.Y
        );
        int thumbY = trackRect.Top + (trackRect.Size.Y - thumbHeight) * Math.Clamp(scrollY, 0, maxScroll) / maxScroll;

        using (ui.Element())
        {
            ui.Color = SettingsTheme.BorderHover;
            ui.Constraints = new Constraints
            {
                X = new Fixed(trackRect.Left),
                Y = new Fixed(thumbY),
                Width = new Fixed(trackRect.Size.X),
                Height = new Fixed(thumbHeight),
            };
            ui.ClipConstraints = new Constraints
            {
                Width = new Fixed(trackRect.Size.X),
                Height = new Fixed(thumbHeight),
            };
        }
    }
}
