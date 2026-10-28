using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Creates one tab at a fixed rectangle and returns true when clicked.</summary>
    public static bool Tab(
        this UIBuilder<Material> ui,
        string id,
        IntRect rect,
        string label,
        bool selected
    )
    {
        return ui.Tab(id, label, selected, RectConstraints(rect));
    }

    /// <summary>Creates one tab in the current layout and returns true when clicked.</summary>
    public static bool Tab(
        this UIBuilder<Material> ui,
        string id,
        string label,
        bool selected,
        Constraints constraints
    )
    {
        using (ui.Element(id))
        {
            bool clicked = ui.Clicked();
            bool hovering = ui.Hovering();

            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = selected ? UiTheme.PanelSolid : UiTheme.PanelInner;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
                Color = selected ? UiTheme.Accent : UiTheme.Border,
            };
            ui.Constraints = constraints;
            ui.ClipConstraints = ClipToBounds();

            if (selected)
            {
                using (ui.Element())
                {
                    ui.Color = UiTheme.PanelSolid;
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Bottom,
                        Width = new Fill(),
                        Height = new Fixed(2),
                    };
                }
            }

            ui.Label(
                label,
                UiMetrics.MENU_SMALL_FONT,
                selected ? UiTheme.AccentBright : hovering ? UiTheme.Text : UiTheme.TextMuted,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                },
                HorizontalAlignment.Center
            );

            return clicked;
        }
    }
}
