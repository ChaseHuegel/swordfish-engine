using System;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Returns the shared menu panel corner caps.</summary>
    public static CornerCapStyle MenuPanelCaps()
    {
        return new CornerCapStyle
        {
            Length = UiMetrics.PANEL_CAP_LENGTH,
            Thickness = UiMetrics.PANEL_CAP_THICKNESS,
            Color = UiTheme.Border,
        };
    }

    /// <summary>Creates a single-outline panel for modal dialogs.</summary>
    public static UIBuilder<Material>.Scope ModalPanel(this UIBuilder<Material> ui, IntRect rect)
    {
        return ui.Panel(
            rect,
            UiTheme.PanelBackground,
            border: default,
            MenuPanelCaps(),
            innerBorder: false
        );
    }

    /// <summary>Creates a menu panel with the shared inner hairline and corner caps.</summary>
    public static UIBuilder<Material>.Scope MenuPanel(
        this UIBuilder<Material> ui,
        IntRect rect,
        string? title = null
    )
    {
        UIBuilder<Material>.Scope scope = ui.Panel(
            rect,
            UiTheme.PanelBackground,
            border: default,
            MenuPanelCaps()
        );
        ui.Border = default;

        if (!string.IsNullOrEmpty(title))
        {
            ui.Label(
                title,
                Rect(0, 12, rect.Size.X, 18),
                UiMetrics.SMALL_FONT,
                UiTheme.TextMuted,
                Anchors.Center
            );
            ui.Divider(
                Rect(12, 32, Math.Max(0, rect.Size.X - 24), 1),
                UiTheme.BorderInner
            );
        }

        return scope;
    }
}
