using System;
using Reef;
using Reef.Constraints;
using Reef.UI;

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
}
