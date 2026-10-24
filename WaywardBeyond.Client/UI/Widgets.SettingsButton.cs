using System;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Settings;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Creates a fixed-position settings button and returns true when clicked.</summary>
    public static bool SettingsButton(
        this UIBuilder<Material> ui,
        string id,
        IntRect rect,
        string text,
        SettingsButtonVariant variant,
        bool enabled = true
    )
    {
        bool clicked;
        bool hovering;

        if (enabled)
        {
            using (ui.Element(id))
            {
                clicked = ui.Clicked();
                hovering = ui.Hovering();
                DrawSettingsButton(ui, rect, text, variant, enabled: true, hovering, clicked);
            }
        }
        else
        {
            using (ui.Element())
            {
                DrawSettingsButton(ui, rect, text, variant, enabled: false, hovering: false, clicked: false);
            }

            clicked = false;
        }

        return clicked;
    }

    private static void DrawSettingsButton(
        UIBuilder<Material> ui,
        IntRect rect,
        string text,
        SettingsButtonVariant variant,
        bool enabled,
        bool hovering,
        bool clicked
    )
    {
        Vector4 fill = SettingsTheme.ButtonBackground;
        Vector4 border = SettingsTheme.Border;
        Vector4 textColor = SettingsTheme.Text;
        var caps = default(CornerCapStyle);

        switch (variant)
        {
            case SettingsButtonVariant.Primary:
                fill = clicked ? SettingsTheme.PrimaryDark : hovering ? SettingsTheme.ButtonHover : SettingsTheme.ButtonBackground;
                border = hovering || clicked ? SettingsTheme.PrimaryBright : SettingsTheme.Primary;
                textColor = hovering || clicked ? SettingsTheme.Text : SettingsTheme.PrimaryBright;
                caps = new CornerCapStyle
                {
                    Length = SettingsMetrics.SelectionCapLength,
                    Thickness = SettingsMetrics.SelectionCapThickness,
                    Color = border,
                };
                break;
            case SettingsButtonVariant.Secondary:
                fill = clicked ? SettingsTheme.AccentDark : hovering ? SettingsTheme.ButtonHover : SettingsTheme.ButtonBackground;
                border = hovering || clicked ? SettingsTheme.BorderHover : SettingsTheme.Border;
                textColor = clicked ? SettingsTheme.AccentBright : SettingsTheme.Text;
                break;
            case SettingsButtonVariant.Subtle:
                fill = clicked ? SettingsTheme.AccentDark : hovering ? SettingsTheme.ButtonHover : SettingsTheme.Transparent;
                border = SettingsTheme.Transparent;
                textColor = clicked ? SettingsTheme.AccentBright : hovering ? SettingsTheme.Text : SettingsTheme.TextMuted;
                break;
        }

        if (!enabled)
        {
            fill = SettingsTheme.PanelInner;
            border = SettingsTheme.BorderInner;
            textColor = SettingsTheme.TextDim;
            caps = default;
        }

        ui.LayoutDirection = LayoutDirection.None;
        ui.Color = fill;
        ui.Border = new BorderStyle
        {
            Thickness = variant == SettingsButtonVariant.Subtle ? 0 : 1,
            Color = border,
        };
        ui.CornerCaps = caps;
        ui.Constraints = SettingsRectConstraints(rect);
        ui.ClipConstraints = SettingsRectConstraints(rect);

        if (variant == SettingsButtonVariant.Primary && enabled)
        {
            using (ui.Element())
            {
                ui.LayoutDirection = LayoutDirection.None;
                ui.Color = SettingsTheme.Transparent;
                ui.Border = new BorderStyle
                {
                    Thickness = 1,
                    Color = SettingsTheme.PrimaryDark,
                };
                IntRect inner = SettingsRect(2, 2, Math.Max(0, rect.Size.X - 4), Math.Max(0, rect.Size.Y - 4));
                ui.Constraints = SettingsRectConstraints(inner);
                ui.ClipConstraints = SettingsRectConstraints(inner);
            }
        }

        using (ui.Text(text))
        {
            ui.FontOptions = new FontOptions
            {
                Size = SettingsMetrics.ButtonFont,
                NoWrap = true,
            };
            ui.Color = textColor;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
        }
    }
}
