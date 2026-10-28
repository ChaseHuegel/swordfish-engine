using System.Numerics;
using Reef.UI;

namespace WaywardBeyond.Client.UI.Theme;

/// <summary>Resolves semantic theme tokens into a concrete control style.</summary>
internal static class UiThemeResolver
{
    /// <summary>The colours one button variant uses across interaction states.</summary>
    private readonly record struct ButtonPalette(
        Vector4 Fill,
        Vector4 HoverFill,
        Vector4 PressedFill,
        Vector4 Border,
        Vector4 ActiveBorder,
        int BorderThickness,
        Vector4 Text,
        Vector4 ActiveText,
        Vector4 PressedText,
        bool Caps
    );

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
                UiMetrics.PANEL_BORDER_THICKNESS,
                default,
                UiTheme.TextDim
            );
        }

        return FromPalette(ResolvePalette(variant), state);
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

    private static ButtonPalette ResolvePalette(ButtonVariant variant)
    {
        return variant switch
        {
            ButtonVariant.Primary => new ButtonPalette(
                UiTheme.ButtonBackground,
                UiTheme.ButtonHover,
                UiTheme.PrimaryDark,
                UiTheme.Primary,
                UiTheme.PrimaryBright,
                UiMetrics.PANEL_BORDER_THICKNESS,
                UiTheme.PrimaryBright,
                UiTheme.Text,
                UiTheme.Text,
                Caps: true
            ),
            ButtonVariant.Danger => new ButtonPalette(
                UiTheme.ButtonBackground,
                UiTheme.ButtonHover,
                UiTheme.DangerDark,
                UiTheme.Danger,
                UiTheme.DangerBright,
                UiMetrics.PANEL_BORDER_THICKNESS,
                UiTheme.DangerBright,
                UiTheme.Text,
                UiTheme.Text,
                Caps: true
            ),
            ButtonVariant.Subtle => new ButtonPalette(
                UiTheme.Transparent,
                UiTheme.ButtonHover,
                UiTheme.AccentDark,
                UiTheme.Transparent,
                UiTheme.Transparent,
                0,
                UiTheme.TextMuted,
                UiTheme.Text,
                UiTheme.AccentBright,
                Caps: false
            ),
            _ => new ButtonPalette(
                UiTheme.ButtonBackground,
                UiTheme.ButtonHover,
                UiTheme.AccentDark,
                UiTheme.Border,
                UiTheme.BorderHover,
                UiMetrics.PANEL_BORDER_THICKNESS,
                UiTheme.Text,
                UiTheme.Text,
                UiTheme.AccentBright,
                Caps: false
            ),
        };
    }

    private static ControlStyle FromPalette(in ButtonPalette palette, ControlState state)
    {
        bool active = state is ControlState.Hover or ControlState.Pressed;

        Vector4 fill = state switch
        {
            ControlState.Pressed => palette.PressedFill,
            ControlState.Hover => palette.HoverFill,
            _ => palette.Fill,
        };

        Vector4 border = active ? palette.ActiveBorder : palette.Border;

        Vector4 text = state switch
        {
            ControlState.Pressed => palette.PressedText,
            ControlState.Hover => palette.ActiveText,
            _ => palette.Text,
        };

        CornerCapStyle caps = palette.Caps
            ? new CornerCapStyle
            {
                Length = UiMetrics.SELECTION_CAP_LENGTH,
                Thickness = UiMetrics.SELECTION_CAP_THICKNESS,
                Color = border,
            }
            : default;

        return new ControlStyle(fill, border, palette.BorderThickness, caps, text);
    }

    private static ControlStyle ResolveIconButton(ButtonVariant variant, ControlState state)
    {
        bool danger = variant == ButtonVariant.IconDanger;

        if (state == ControlState.Disabled)
        {
            return new ControlStyle(UiTheme.Transparent, UiTheme.Transparent, 0, default, UiTheme.TextDim);
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

        bool active = state is ControlState.Hover or ControlState.Pressed;
        Vector4 activeText = danger ? UiTheme.DangerBright : UiTheme.Text;

        Vector4 fill = state switch
        {
            ControlState.Pressed => danger ? UiTheme.DangerDark : UiTheme.AccentDark,
            ControlState.Hover => danger ? UiTheme.DangerDark : UiTheme.ButtonHover,
            _ => UiTheme.Transparent,
        };

        return new ControlStyle(
            fill,
            UiTheme.Transparent,
            0,
            default,
            active ? activeText : danger ? UiTheme.Danger : UiTheme.TextMuted
        );
    }
}
