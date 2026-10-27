using System;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Draws a titled information box with wrapped body text.</summary>
    public static void InfoBox(
        this UIBuilder<Material> ui,
        IntRect rect,
        string title,
        string body,
        string? highlightPrefix = null,
        int bodyFontSize = 0
    )
    {
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = UiTheme.PanelInner;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
                Color = UiTheme.Border,
            };
            ui.Constraints = RectConstraints(rect);
            ui.ClipConstraints = RectConstraints(rect);

            ui.Label(
                title,
                Rect(10, 4, Math.Max(0, rect.Size.X - 20), 16),
                UiMetrics.INFO_BOX_TITLE_FONT,
                UiTheme.Text,
                Anchors.Left | Anchors.Center
            );
            ui.Divider(Rect(10, 21, Math.Max(0, rect.Size.X - 20), 1), UiTheme.Border);

            string bodyText = highlightPrefix != null
                ? $"#F2994AFF {highlightPrefix} #R {body}"
                : body;

            ui.Paragraph(
                bodyText,
                Rect(10, 27, Math.Max(0, rect.Size.X - 20), Math.Max(0, rect.Size.Y - 35)),
                bodyFontSize > 0 ? bodyFontSize : UiMetrics.MENU_SMALL_FONT,
                UiTheme.Text
            );
        }
    }
}
