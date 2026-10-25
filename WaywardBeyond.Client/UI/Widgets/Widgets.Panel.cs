using System;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Creates a clipped panel with a border, corner caps, and an optional inner hairline.</summary>
    public static UIBuilder<Material>.Scope Panel(
        this UIBuilder<Material> ui,
        IntRect rect,
        Vector4 fill,
        BorderStyle border,
        CornerCapStyle caps,
        bool innerBorder = true
    )
    {
        UIBuilder<Material>.Scope scope = ui.Element();
        ui.LayoutDirection = LayoutDirection.None;
        ui.Color = fill;
        ui.Border = border;
        ui.CornerCaps = caps;
        ui.Constraints = RectConstraints(rect);
        ui.ClipConstraints = RectConstraints(rect);

        if (innerBorder)
        {
            using (ui.Element())
            {
                ui.LayoutDirection = LayoutDirection.None;
                ui.Color = UiTheme.Transparent;
                ui.Border = new BorderStyle
                {
                    Thickness = UiMetrics.PanelInnerBorderThickness,
                    Color = UiTheme.BorderInner,
                };
                int inset = UiMetrics.PanelInnerInset;
                IntRect inner = Rect(inset, inset, Math.Max(0, rect.Size.X - inset * 2), Math.Max(0, rect.Size.Y - inset * 2));
                ui.Constraints = RectConstraints(inner);
                ui.ClipConstraints = RectConstraints(inner);
            }
        }

        return scope;
    }
}
