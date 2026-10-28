using System;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Draws a non-interactive scrollbar that fills the current layout.</summary>
    public static void Scrollbar(
        this UIBuilder<Material> ui,
        int contentHeight,
        int viewportHeight,
        int scrollY,
        int maxScroll,
        int width
    )
    {
        if (contentHeight <= 0)
        {
            return;
        }

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = UiTheme.Track;
            ui.Constraints = new Constraints
            {
                Width = new Fixed(width),
                Height = new Fill(),
            };
            ui.ClipConstraints = ClipToBounds();

            if (maxScroll <= 0 || viewportHeight <= 0)
            {
                return;
            }

            float visibleRatio = Math.Clamp((float)viewportHeight / contentHeight, 0f, 1f);
            float minRatio = Math.Min(
                1f,
                UiMetrics.SCROLLBAR_THUMB_MIN_HEIGHT / (float)viewportHeight
            );
            float thumbRatio = Math.Max(visibleRatio, minRatio);
            float travel = Math.Max(0f, 1f - thumbRatio);
            float progress = Math.Clamp(scrollY / (float)maxScroll, 0f, 1f);

            using (ui.Element())
            {
                ui.Color = UiTheme.BorderHover;
                ui.Constraints = new Constraints
                {
                    Y = new Relative(progress * travel),
                    Width = new Fill(),
                    Height = new Relative(thumbRatio),
                };
            }
        }
    }

    /// <summary>Draws a non-interactive scrollbar for a clipped list.</summary>
    public static void Scrollbar(
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
            ui.Color = UiTheme.Track;
            ui.Constraints = RectConstraints(trackRect);
            ui.ClipConstraints = ClipConstraints(trackRect);
        }

        int thumbHeight = Math.Clamp(
            trackRect.Size.Y * viewportHeight / contentHeight,
            UiMetrics.SCROLLBAR_THUMB_MIN_HEIGHT,
            trackRect.Size.Y
        );
        int thumbY = trackRect.Top + (trackRect.Size.Y - thumbHeight) * Math.Clamp(scrollY, 0, maxScroll) / maxScroll;

        using (ui.Element())
        {
            ui.Color = UiTheme.BorderHover;
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
