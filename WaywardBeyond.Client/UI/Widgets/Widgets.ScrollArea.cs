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
    /// <summary>Draws a clipped scroll area and its scrollbar.</summary>
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
            scrollY -= (int)MathF.Round(wheel * UiMetrics.ScrollStep);
        }

        scrollY = Math.Clamp(scrollY, 0, maxScroll);

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = UiTheme.Transparent;
            ui.Constraints = RectConstraints(viewport);
            ui.ClipConstraints = new Constraints
            {
                Width = new Fixed(viewport.Size.X),
                Height = new Fixed(viewport.Size.Y),
            };
            ui.ScrollY = -scrollY;
            drawContent(ui);
        }

        ui.Scrollbar(scrollbarRect, contentHeight, viewport.Size.Y, scrollY);
    }
}
