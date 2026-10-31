using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Creates fixed-position constraints for a rectangle.</summary>
    public static Constraints RectConstraints(IntRect rect)
    {
        return new Constraints
        {
            X = new Fixed(rect.Left),
            Y = new Fixed(rect.Top),
            Width = new Fixed(rect.Size.X),
            Height = new Fixed(rect.Size.Y),
        };
    }

    /// <summary>Creates clip constraints that cover the element's own bounds.</summary>
    public static Constraints ClipToBounds()
    {
        return new Constraints
        {
            Width = new Relative(1f),
            Height = new Relative(1f),
        };
    }

    /// <summary>Opens a horizontal stack.</summary>
    public static UIBuilder<Material>.Scope Row(
        this UIBuilder<Material> ui,
        Constraints constraints,
        int spacing = 0,
        string? id = null
    )
    {
        UIBuilder<Material>.Scope scope = id == null ? ui.Element() : ui.Element(id);
        ui.LayoutDirection = LayoutDirection.Horizontal;
        ui.Spacing = spacing;
        ui.Constraints = constraints;
        return scope;
    }

    /// <summary>Draws a single-line label in the current layout.</summary>
    public static void Label(
        this UIBuilder<Material> ui,
        string text,
        int fontSize,
        Vector4 color,
        Constraints constraints,
        HorizontalAlignment horizontal = HorizontalAlignment.Left,
        VerticalAlignment vertical = VerticalAlignment.Center,
        string? fontId = null
    )
    {
        using (ui.Text(text))
        {
            ui.FontOptions = new FontOptions
            {
                ID = fontId ?? "Salmon Mono 9 Regular",
                Size = fontSize,
                NoWrap = true,
                HorizontalAlignment = horizontal,
                VerticalAlignment = vertical,
            };
            ui.Color = color;
            ui.Constraints = constraints;
        }
    }

    private static Vector4 FromRgb(byte r, byte g, byte b, byte a = 255)
    {
        return new Vector4(r / 255f, g / 255f, b / 255f, a / 255f);
    }
}
