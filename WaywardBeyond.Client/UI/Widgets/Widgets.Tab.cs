using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Creates one tab and returns true when clicked.</summary>
    public static bool Tab(
        this UIBuilder<Material> ui,
        string id,
        IntRect rect,
        string label,
        bool selected
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
                Thickness = UiMetrics.PanelBorderThickness,
                Color = selected ? UiTheme.BorderHover : UiTheme.Border,
            };
            ui.Constraints = RectConstraints(rect);
            ui.ClipConstraints = RectConstraints(rect);

            if (selected && rect.Size.Y > 2)
            {
                using (ui.Element())
                {
                    ui.Color = UiTheme.PanelSolid;
                    ui.Constraints = new Constraints
                    {
                        X = new Fixed(1),
                        Y = new Fixed(rect.Size.Y - 2),
                        Width = new Fixed(System.Math.Max(0, rect.Size.X - 2)),
                        Height = new Fixed(2),
                    };
                }
            }

            ui.Label(
                label,
                Rect(0, 0, rect.Size.X, rect.Size.Y),
                UiMetrics.MenuSmallFont,
                selected ? UiTheme.AccentBright : hovering ? UiTheme.Text : UiTheme.TextMuted,
                Anchors.Center
            );

            return clicked;
        }
    }
}
