using System;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Draws a clipped, wheel-scrolled area and its scrollbar inside a fixed viewport.</summary>
    public static void ScrollArea(
        this UIBuilder<Material> ui,
        IntRect viewport,
        int contentHeight,
        ref int scrollY,
        IInputService inputService,
        IntRect scrollbarRect,
        Action<UIBuilder<Material>> drawContent
    )
    {
        int maxScroll = Math.Max(0, contentHeight - viewport.Size.Y);
        float wheel = inputService.GetMouseScroll();
        if (wheel != 0f)
        {
            scrollY -= (int)MathF.Round(wheel * UiMetrics.SCROLL_STEP);
        }

        scrollY = Math.Clamp(scrollY, 0, maxScroll);

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = UiTheme.Transparent;
            ui.Constraints = RectConstraints(viewport);
            ui.ClipConstraints = ClipConstraints(viewport);
            ui.ScrollY = -scrollY;
            drawContent(ui);
        }

        ui.Scrollbar(scrollbarRect, contentHeight, viewport.Size.Y, scrollY);
    }

    /// <summary>Draws a clipped, wheel-scrolled area that fills the current layout.</summary>
    public static void ScrollArea(
        this UIBuilder<Material> ui,
        string id,
        int contentHeight,
        ref int scrollY,
        IInputService inputService,
        Action<UIBuilder<Material>> drawContent,
        int scrollbarWidth = 6,
        int scrollbarGap = 8
    )
    {
        int viewportHeight = 0;
        int maxScroll = 0;

        using (ui.Row(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            },
            spacing: scrollbarGap
        ))
        {
            using (ui.Element(id))
            {
                viewportHeight = ui.GetElementBounds().Rect.Size.Y;
                maxScroll = viewportHeight > 0
                    ? Math.Max(0, contentHeight - viewportHeight)
                    : Math.Max(0, contentHeight);

                float delta = ui.GetElementScrollDelta();
                if (delta == 0f && ui.Hovering())
                {
                    delta = inputService.GetMouseScroll();
                }

                if (delta != 0f)
                {
                    scrollY -= (int)MathF.Round(delta * UiMetrics.SCROLL_STEP);
                }

                scrollY = Math.Clamp(scrollY, 0, maxScroll);

                ui.LayoutDirection = LayoutDirection.Vertical;
                ui.VerticalScroll = true;
                ui.ScrollY = -scrollY;
                ui.Constraints = new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                };
                ui.ClipConstraints = ClipToBounds();
                drawContent(ui);
            }

            ui.Scrollbar(
                contentHeight,
                viewportHeight,
                scrollY,
                maxScroll,
                scrollbarWidth
            );
        }
    }
}
