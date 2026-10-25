using Reef.UI;

namespace WaywardBeyond.Client.UI.Theme;

/// <summary>Resolves semantic theme tokens into a concrete control style.</summary>
internal static class UiThemeResolver
{
    /// <summary>Resolves a button style for an interaction state.</summary>
    public static ControlStyle ResolveButton(ButtonVariant variant, bool enabled, bool hovering, bool pressed)
    {
        ControlState state = !enabled
            ? ControlState.Disabled
            : pressed
                ? ControlState.Pressed
                : hovering
                    ? ControlState.Hover
                    : ControlState.Normal;

        return ResolveButton(variant, state);
    }

    /// <summary>Resolves a button style for an explicit state.</summary>
    public static ControlStyle ResolveButton(ButtonVariant variant, ControlState state)
    {
        if (variant is ButtonVariant.Icon or ButtonVariant.IconDanger)
        {
            return ResolveIconButton(variant, state);
        }

        if (state == ControlState.Disabled)
        {
            return new ControlStyle(
                UiTheme.PanelInner,
                UiTheme.BorderInner,
                UiMetrics.PanelBorderThickness,
                default,
                UiTheme.TextDim
            );
        }

        var caps = default(CornerCapStyle);

        switch (variant)
        {
            case ButtonVariant.Primary:
                caps = new CornerCapStyle
                {
                    Length = UiMetrics.SelectionCapLength,
                    Thickness = UiMetrics.SelectionCapThickness,
                    Color = state is ControlState.Hover or ControlState.Pressed ? UiTheme.PrimaryBright : UiTheme.Primary,
                };
                return new ControlStyle(
                    state == ControlState.Pressed ? UiTheme.PrimaryDark : state == ControlState.Hover ? UiTheme.ButtonHover : UiTheme.ButtonBackground,
                    state is ControlState.Hover or ControlState.Pressed ? UiTheme.PrimaryBright : UiTheme.Primary,
                    UiMetrics.PanelBorderThickness,
                    caps,
                    state is ControlState.Hover or ControlState.Pressed ? UiTheme.Text : UiTheme.PrimaryBright
                );
            case ButtonVariant.Danger:
                caps = new CornerCapStyle
                {
                    Length = UiMetrics.SelectionCapLength,
                    Thickness = UiMetrics.SelectionCapThickness,
                    Color = state is ControlState.Hover or ControlState.Pressed ? UiTheme.DangerBright : UiTheme.Danger,
                };
                return new ControlStyle(
                    state == ControlState.Pressed ? UiTheme.DangerDark : state == ControlState.Hover ? UiTheme.ButtonHover : UiTheme.ButtonBackground,
                    state is ControlState.Hover or ControlState.Pressed ? UiTheme.DangerBright : UiTheme.Danger,
                    UiMetrics.PanelBorderThickness,
                    caps,
                    state is ControlState.Hover or ControlState.Pressed ? UiTheme.Text : UiTheme.DangerBright
                );
            case ButtonVariant.Subtle:
                return new ControlStyle(
                    state == ControlState.Pressed ? UiTheme.AccentDark : state == ControlState.Hover ? UiTheme.ButtonHover : UiTheme.Transparent,
                    UiTheme.Transparent,
                    0,
                    default,
                    state == ControlState.Pressed ? UiTheme.AccentBright : state == ControlState.Hover ? UiTheme.Text : UiTheme.TextMuted
                );
            default:
                return new ControlStyle(
                    state == ControlState.Pressed ? UiTheme.AccentDark : state == ControlState.Hover ? UiTheme.ButtonHover : UiTheme.ButtonBackground,
                    state is ControlState.Hover or ControlState.Pressed ? UiTheme.BorderHover : UiTheme.Border,
                    UiMetrics.PanelBorderThickness,
                    default,
                    state == ControlState.Pressed ? UiTheme.AccentBright : UiTheme.Text
                );
        }
    }

    private static ControlStyle ResolveIconButton(ButtonVariant variant, ControlState state)
    {
        bool danger = variant == ButtonVariant.IconDanger;

        if (state == ControlState.Disabled)
        {
            return new ControlStyle(
                UiTheme.Transparent,
                UiTheme.Transparent,
                0,
                default,
                UiTheme.TextDim
            );
        }

        if (state == ControlState.Selected)
        {
            return new ControlStyle(
                UiTheme.AccentDark,
                UiTheme.Transparent,
                0,
                default,
                danger ? UiTheme.DangerBright : UiTheme.AccentBright
            );
        }

        return new ControlStyle(
            state == ControlState.Pressed
                ? danger ? UiTheme.DangerDark : UiTheme.AccentDark
                : state == ControlState.Hover
                    ? danger ? UiTheme.DangerDark : UiTheme.ButtonHover
                    : UiTheme.Transparent,
            UiTheme.Transparent,
            0,
            default,
            danger
                ? state is ControlState.Hover or ControlState.Pressed ? UiTheme.DangerBright : UiTheme.Danger
                : state is ControlState.Hover or ControlState.Pressed ? UiTheme.Text : UiTheme.TextMuted
        );
    }

    /// <summary>Resolves the style of one cell in a segmented control.</summary>
    public static ControlStyle ResolveChoiceCell(bool selected, bool enabled, bool hovering)
    {
        if (!enabled)
        {
            return new ControlStyle(UiTheme.ControlBackground, UiTheme.Transparent, 0, default, UiTheme.TextDim);
        }

        if (selected)
        {
            return new ControlStyle(UiTheme.AccentDark, UiTheme.Transparent, 0, default, UiTheme.AccentBright);
        }

        return new ControlStyle(
            hovering ? UiTheme.ButtonHover : UiTheme.ControlBackground,
            UiTheme.Transparent,
            0,
            default,
            hovering ? UiTheme.Text : UiTheme.TextMuted
        );
    }

    /// <summary>Resolves the style of a stepper arrow.</summary>
    public static ControlStyle ResolveStepperArrow(bool enabled, bool hovering)
    {
        return new ControlStyle(
            hovering && enabled ? UiTheme.ButtonHover : UiTheme.Transparent,
            UiTheme.Transparent,
            0,
            default,
            !enabled
                ? UiTheme.TextDim
                : hovering
                    ? UiTheme.AccentBright
                    : UiTheme.TextMuted
        );
    }
}
