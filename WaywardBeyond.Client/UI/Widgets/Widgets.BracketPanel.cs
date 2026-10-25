using System;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Returns the heavy corner caps used by dossier panels.</summary>
    public static CornerCapStyle BracketCaps()
    {
        return new CornerCapStyle
        {
            Length = UiMetrics.BracketCapLength,
            Thickness = UiMetrics.BracketCapThickness,
            Color = UiTheme.Bracket,
        };
    }

    /// <summary>Creates a dossier panel with heavy corner brackets and a title.</summary>
    public static UIBuilder<Material>.Scope BracketPanel(
        this UIBuilder<Material> ui,
        IntRect rect,
        string? title = null
    )
    {
        UIBuilder<Material>.Scope scope = ui.Element();
        ui.LayoutDirection = LayoutDirection.None;
        ui.Color = UiTheme.PanelBackground;
        ui.CornerCaps = BracketCaps();
        ui.Constraints = RectConstraints(rect);
        ui.ClipConstraints = RectConstraints(rect);

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = UiTheme.Transparent;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PanelBorderThickness,
                Color = UiTheme.Border,
            };
            IntRect inner = Rect(2, 2, Math.Max(0, rect.Size.X - 4), Math.Max(0, rect.Size.Y - 4));
            ui.Constraints = RectConstraints(inner);
            ui.ClipConstraints = RectConstraints(inner);
        }

        if (!string.IsNullOrEmpty(title))
        {
            ui.Label(
                title,
                Rect(12, 8, Math.Max(0, rect.Size.X - 24), 16),
                UiMetrics.MenuSmallFont,
                UiTheme.Text,
                Anchors.Center
            );
        }

        return scope;
    }
}
