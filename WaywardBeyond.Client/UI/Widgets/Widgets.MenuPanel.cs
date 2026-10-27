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
    /// <summary>Represents an active modal dialog layout scope encompassing scrim, center container, and modal panel.</summary>
    public ref struct ModalScope(
        UIBuilder<Material>.Scope scrim,
        UIBuilder<Material>.Scope center,
        UIBuilder<Material>.Scope panel
    ) {
        private UIBuilder<Material>.Scope _scrim = scrim;
        private UIBuilder<Material>.Scope _center = center;
        private UIBuilder<Material>.Scope _panel = panel;

        public void Dispose()
        {
            _panel.Dispose();
            _center.Dispose();
            _scrim.Dispose();
        }
    }

    /// <summary>Creates a centered modal dialog with scrim background, centered container, and modal panel.</summary>
    public static ModalScope ModalDialog(
        this UIBuilder<Material> ui,
        int width,
        int height,
        Vector4? scrimColor = null
    ) {
        UIBuilder<Material>.Scope scrim = ui.Element();
        ui.LayoutDirection = LayoutDirection.None;
        ui.Color = scrimColor ?? UiTheme.Scrim;
        ui.Constraints = new Constraints
        {
            Width = new Fill(),
            Height = new Fill(),
        };

        UIBuilder<Material>.Scope center = ui.Element();
        ui.LayoutDirection = LayoutDirection.None;
        ui.Constraints = new Constraints
        {
            Anchors = Anchors.Center,
            Width = new Fixed(width),
            Height = new Fixed(height),
        };
        ui.ClipConstraints = new Constraints
        {
            Width = new Fixed(width),
            Height = new Fixed(height),
        };

        UIBuilder<Material>.Scope panel = ui.ModalPanel(Rect(0, 0, width, height));

        return new ModalScope(scrim, center, panel);
    }

    /// <summary>Returns the shared menu panel corner caps.</summary>
    public static CornerCapStyle MenuPanelCaps()
    {
        return new CornerCapStyle
        {
            Length = UiMetrics.PanelCapLength,
            Thickness = UiMetrics.PanelCapThickness,
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
                UiMetrics.SmallFont,
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
