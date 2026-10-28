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
    /// <summary>Creates a rectangle from a position and size.</summary>
    public static IntRect Rect(int left, int top, int width, int height)
    {
        return new IntRect(new IntVector2(left, top), new IntVector2(width, height));
    }

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

    /// <summary>Creates clip constraints that cover an element of a given size.</summary>
    public static Constraints ClipConstraints(IntRect rect)
    {
        return new Constraints
        {
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

    /// <summary>Opens a centered page with a fixed maximum size and its own clip.</summary>
    public static UIBuilder<Material>.Scope Page(
        this UIBuilder<Material> ui,
        int width,
        int height,
        string? id = null
    )
    {
        UIBuilder<Material>.Scope scope = id == null ? ui.Element() : ui.Element(id);
        ui.LayoutDirection = LayoutDirection.Vertical;
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
        return scope;
    }

    /// <summary>Opens a vertical stack.</summary>
    public static UIBuilder<Material>.Scope Column(
        this UIBuilder<Material> ui,
        Constraints constraints,
        int spacing = 0,
        string? id = null
    )
    {
        UIBuilder<Material>.Scope scope = id == null ? ui.Element() : ui.Element(id);
        ui.LayoutDirection = LayoutDirection.Vertical;
        ui.Spacing = spacing;
        ui.Constraints = constraints;
        return scope;
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

    /// <summary>Adds a fixed-size empty element.</summary>
    public static void Spacer(this UIBuilder<Material> ui, int width = 0, int height = 0)
    {
        if (width <= 0 && height <= 0)
        {
            return;
        }

        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Width = width > 0 ? new Fixed(width) : null,
                Height = height > 0 ? new Fixed(height) : null,
            };
        }
    }

    /// <summary>Adds an empty element that consumes the remaining space.</summary>
    public static void Filler(this UIBuilder<Material> ui)
    {
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            };
        }
    }

    /// <summary>Insets a rectangle on every side.</summary>
    public static IntRect Inset(IntRect rect, int inset)
    {
        return new IntRect(
            rect.Left + inset,
            rect.Top + inset,
            Math.Max(rect.Left + inset, rect.Right - inset),
            Math.Max(rect.Top + inset, rect.Bottom - inset)
        );
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
                ID = fontId ?? UiTheme.FONT_UI,
                Size = fontSize,
                NoWrap = true,
                HorizontalAlignment = horizontal,
                VerticalAlignment = vertical,
            };
            ui.Color = color;
            ui.Constraints = constraints;
        }
    }

    /// <summary>Draws wrapped text in the current layout.</summary>
    public static void Paragraph(
        this UIBuilder<Material> ui,
        string text,
        int fontSize,
        Vector4 color,
        Constraints constraints,
        HorizontalAlignment horizontal = HorizontalAlignment.Left,
        string? fontId = null
    )
    {
        using (ui.Text(text))
        {
            ui.FontOptions = new FontOptions
            {
                ID = fontId ?? UiTheme.FONT_UI,
                Size = fontSize,
                HorizontalAlignment = horizontal,
            };
            ui.Color = color;
            ui.Constraints = constraints;
        }
    }
}
