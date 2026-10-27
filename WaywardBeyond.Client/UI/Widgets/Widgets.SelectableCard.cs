using System;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Creates a clickable card with selected and hover styling.</summary>
    public static bool SelectableCard(
        this UIBuilder<Material> ui,
        string id,
        IntRect rect,
        SelectableCardOptions options,
        Action<UIBuilder<Material>> drawContent
    )
    {
        return ui.SelectableCard(id, rect, in options, drawContent, static (contentUi, draw) => draw(contentUi));
    }

    /// <summary>Creates a clickable card in the current layout.</summary>
    public static bool SelectableCard(
        this UIBuilder<Material> ui,
        string id,
        Constraints constraints,
        SelectableCardOptions options,
        Action<UIBuilder<Material>> drawContent
    )
    {
        return ui.SelectableCard(id, constraints, options, drawContent, static (contentUi, draw) => draw(contentUi));
    }

    /// <summary>Creates a clickable card and draws its content from an explicit state value.</summary>
    public static bool SelectableCard<TState>(
        this UIBuilder<Material> ui,
        string id,
        IntRect rect,
        in SelectableCardOptions options,
        TState state,
        Action<UIBuilder<Material>, TState> drawContent
    )
    {
        return ui.SelectableCard(
            id,
            RectConstraints(rect),
            options,
            state,
            drawContent
        );
    }

    /// <summary>Creates a clickable card in the current layout and draws its content from an explicit state value.</summary>
    public static bool SelectableCard<TState>(
        this UIBuilder<Material> ui,
        string id,
        Constraints constraints,
        in SelectableCardOptions options,
        TState state,
        Action<UIBuilder<Material>, TState> drawContent
    )
    {
        bool clicked;
        bool hovering;

        if (!options.Enabled)
        {
            using (ui.Element())
            {
                DrawSelectableCard(ui, constraints, options, enabled: false, hovering: false, pressed: false);
                drawContent(ui, state);
            }

            return false;
        }

        using (ui.Element(id))
        {
            clicked = ui.Clicked();
            hovering = ui.Hovering();
            DrawSelectableCard(ui, constraints, options, enabled: true, hovering, clicked);
            drawContent(ui, state);
        }

        if (clicked && options.SoundEffectService != null)
        {
            Interactions.Click.WithButtonAudio(options.SoundEffectService);
        }

        return clicked;
    }

    private static void DrawSelectableCard(
        UIBuilder<Material> ui,
        Constraints constraints,
        SelectableCardOptions options,
        bool enabled,
        bool hovering,
        bool pressed
    )
    {
        ui.LayoutDirection = LayoutDirection.None;
        ui.Color = !enabled
            ? UiTheme.PanelInner
            : options.Selected
                ? UiTheme.SelectionBackground
                : hovering || pressed
                    ? UiTheme.ButtonHover
                    : UiTheme.PanelInner;
        ui.Border = new BorderStyle
        {
            Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
            Color = !enabled
                ? UiTheme.BorderInner
                : options.Selected
                    ? UiTheme.Primary
                    : hovering || pressed
                        ? UiTheme.BorderHover
                        : UiTheme.Border,
        };
        ui.CornerCaps = default;
        ui.Constraints = constraints;
        ui.Padding = new Padding(left: 0, top: 6, right: 0, bottom: 6);
        ui.ClipConstraints = ClipToBounds();

        if (options.Selected)
        {
            using (ui.Element())
            {
                ui.LayoutDirection = LayoutDirection.None;
                ui.Color = UiTheme.Accent;
                ui.Constraints = new Constraints
                {
                    X = new Fixed(2),
                    Width = new Fixed(3),
                    Height = new Fill(),
                };
            }
        }
    }

    /// <summary>Options for a selectable card.</summary>
    public readonly record struct SelectableCardOptions(
        bool Selected = false,
        bool Enabled = true,
        SoundEffectService? SoundEffectService = null
    );
}
