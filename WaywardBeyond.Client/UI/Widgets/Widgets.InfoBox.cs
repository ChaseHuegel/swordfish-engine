using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Draws a titled information box with wrapped body text inside a fixed rectangle.</summary>
    public static void InfoBox(
        this UIBuilder<Material> ui,
        IntRect rect,
        string title,
        string body,
        string? highlightPrefix = null,
        int bodyFontSize = 0
    )
    {
        ui.InfoBox(RectConstraints(rect), title, body, highlightPrefix, bodyFontSize);
    }

    /// <summary>Draws a titled information box with wrapped body text in the current layout.</summary>
    public static void InfoBox(
        this UIBuilder<Material> ui,
        Constraints constraints,
        string title,
        string body,
        string? highlightPrefix = null,
        int bodyFontSize = 0
    )
    {
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Padding = new Padding(10);
            ui.Color = UiTheme.PanelInner;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
                Color = UiTheme.Border,
            };
            ui.Constraints = constraints;
            ui.ClipConstraints = ClipToBounds();

            ui.Label(
                title,
                UiMetrics.INFO_BOX_TITLE_FONT,
                UiTheme.Text,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(16),
                }
            );
            ui.Spacer(height: 4);
            ui.Divider(
                UiTheme.Border,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(1),
                }
            );
            ui.Spacer(height: 6);

            string bodyText = highlightPrefix != null
                ? $"#F2994AFF {highlightPrefix} #R {body}"
                : body;

            ui.Paragraph(
                bodyText,
                bodyFontSize > 0 ? bodyFontSize : UiMetrics.MENU_SMALL_FONT,
                UiTheme.Text,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                }
            );
        }
    }
}
