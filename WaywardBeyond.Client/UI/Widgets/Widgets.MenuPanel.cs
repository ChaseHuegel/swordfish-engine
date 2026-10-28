using System;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Holds the background and content scopes of one menu panel.</summary>
    public readonly ref struct MenuPanelScope(
        UIBuilder<Material>.Scope background,
        UIBuilder<Material>.Scope? content
    ) : IDisposable {
        private readonly UIBuilder<Material>.Scope _background = background;
        private readonly UIBuilder<Material>.Scope? _content = content;

        public void Dispose()
        {
            _content?.Dispose();
            _background.Dispose();
        }
    }

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
    public static MenuPanelScope ModalPanel(
        this UIBuilder<Material> ui,
        Constraints constraints,
        int padding = UiMetrics.MODAL_PADDING
    )
    {
        return ui.MenuPanel(
            constraints,
            title: null,
            padding,
            innerBorder: false
        );
    }

    /// <summary>Creates a single-outline panel for modal dialogs at a fixed rectangle.</summary>
    public static MenuPanelScope ModalPanel(this UIBuilder<Material> ui, IntRect rect)
    {
        return ui.ModalPanel(RectConstraints(rect));
    }

    /// <summary>Creates a menu panel with the shared inner hairline and corner caps.</summary>
    /// <remarks>The content hugs its children when the panel width is not constrained.</remarks>
    public static MenuPanelScope MenuPanel(
        this UIBuilder<Material> ui,
        Constraints constraints,
        string? title = null,
        int padding = 12,
        bool innerBorder = true
    )
    {
        UIBuilder<Material>.Scope background = ui.Element();
        ui.LayoutDirection = LayoutDirection.None;
        ui.Color = UiTheme.PanelBackground;
        ui.Border = default;
        ui.CornerCaps = MenuPanelCaps();
        ui.Constraints = constraints;

        if (!innerBorder && string.IsNullOrEmpty(title))
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Padding = new Padding(padding);
            ui.ClipConstraints = ClipToBounds();
            return new MenuPanelScope(background, content: null);
        }

        bool hugWidth = constraints.Width is null or Fit or Shrink;
        int insetWidth = UiMetrics.PANEL_INNER_INSET * 2;

        ui.Padding = innerBorder ? new Padding(UiMetrics.PANEL_INNER_INSET) : new Padding(0);

        UIBuilder<Material>.Scope content = ui.Element();
        ui.LayoutDirection = LayoutDirection.Vertical;
        ui.Padding = new Padding(padding);
        ui.Color = UiTheme.Transparent;
        ui.Border = innerBorder
            ? new BorderStyle
            {
                Thickness = UiMetrics.PANEL_INNER_BORDER_THICKNESS,
                Color = UiTheme.BorderInner,
            }
            : default;
        ui.Constraints = new Constraints
        {
            Width = hugWidth ? null : new Fill(),
            Height = new Fill(),
            MinWidth = hugWidth ? Math.Max(0, constraints.MinWidth - insetWidth) : 0,
            MaxWidth = hugWidth ? Math.Max(0, constraints.MaxWidth - insetWidth) : 0,
        };
        ui.ClipConstraints = ClipToBounds();

        if (!string.IsNullOrEmpty(title))
        {
            ui.Label(
                title,
                UiMetrics.SMALL_FONT,
                UiTheme.TextMuted,
                new Constraints
                {
                    Width = hugWidth ? null : new Fill(),
                    Height = new Fixed(18),
                },
                HorizontalAlignment.Center
            );
            ui.Spacer(height: 4);
            ui.Divider(
                UiTheme.BorderInner,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(1),
                }
            );
            ui.Spacer(height: 8);
        }

        return new MenuPanelScope(background, content);
    }

    /// <summary>Creates a menu panel with the shared inner hairline and corner caps at a fixed rectangle.</summary>
    public static MenuPanelScope MenuPanel(
        this UIBuilder<Material> ui,
        IntRect rect,
        string? title = null
    )
    {
        return ui.MenuPanel(RectConstraints(rect), title);
    }
}
