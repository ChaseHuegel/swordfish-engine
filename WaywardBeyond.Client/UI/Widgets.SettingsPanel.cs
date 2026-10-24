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
    /// <summary>Creates a clipped settings panel with a border, corner caps, and an optional inner hairline.</summary>
    public static UIBuilder<Material>.Scope SettingsPanel(
        this UIBuilder<Material> ui,
        IntRect rect,
        Vector4 fill,
        BorderStyle border,
        CornerCapStyle caps,
        bool innerBorder = true
    )
    {
        UIBuilder<Material>.Scope scope = ui.Element();
        ui.LayoutDirection = LayoutDirection.None;
        ui.Color = fill;
        ui.Border = border;
        ui.CornerCaps = caps;
        ui.Constraints = SettingsRectConstraints(rect);
        ui.ClipConstraints = SettingsRectConstraints(rect);

        if (innerBorder)
        {
            using (ui.Element())
            {
                ui.LayoutDirection = LayoutDirection.None;
                ui.Color = SettingsTheme.Transparent;
                ui.Border = new BorderStyle
                {
                    Thickness = 1,
                    Color = SettingsTheme.BorderInner,
                };
                IntRect inner = SettingsRect(2, 2, Math.Max(0, rect.Size.X - 4), Math.Max(0, rect.Size.Y - 4));
                ui.Constraints = SettingsRectConstraints(inner);
                ui.ClipConstraints = SettingsRectConstraints(inner);
            }
        }

        return scope;
    }

    /// <summary>Creates a rectangle from a position and size.</summary>
    public static IntRect SettingsRect(int left, int top, int width, int height)
    {
        return new IntRect(new IntVector2(left, top), new IntVector2(width, height));
    }

    /// <summary>Creates fixed-position constraints for a rectangle.</summary>
    public static Constraints SettingsRectConstraints(IntRect rect)
    {
        return new Constraints
        {
            X = new Fixed(rect.Left),
            Y = new Fixed(rect.Top),
            Width = new Fixed(rect.Size.X),
            Height = new Fixed(rect.Size.Y),
        };
    }

    /// <summary>Insets a rectangle on every side.</summary>
    public static IntRect SettingsInset(IntRect rect, int inset)
    {
        return new IntRect(
            rect.Left + inset,
            rect.Top + inset,
            Math.Max(rect.Left + inset, rect.Right - inset),
            Math.Max(rect.Top + inset, rect.Bottom - inset)
        );
    }
}
