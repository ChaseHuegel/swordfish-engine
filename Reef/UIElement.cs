using System.Collections.Generic;
using System.Numerics;

namespace Reef;

public struct UIElement
{
    public IntRect Rect;
    public Style Style;
    public Layout Layout;
    public Constraints Constraints;
    public List<UIElement>? Children;
}

public struct Constraints
{
    public IConstraint? X;
    public IConstraint? Y;
    public IConstraint? Width;
    public IConstraint? Height;
}

public struct Style
{
    public Vector4 BackgroundColor;
    public Padding Padding;
    public CornerRadius CornerRadius;
}

public struct Layout
{
    public LayoutDirection Direction;
    public int Spacing;
}

public struct Padding
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public Padding(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public Padding(int value)
    {
        Left = value;
        Top = value;
        Right = value;
        Bottom = value;
    }
}

public struct CornerRadius
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public CornerRadius(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public CornerRadius(int value)
    {
        Left = value;
        Top = value;
        Right = value;
        Bottom = value;
    }
}

public enum LayoutDirection
{
    Horizontal,
    Vertical,
}

public interface IConstraint
{
    int Calculate(int value);
}

public struct Fit : IConstraint
{
    public int Calculate(int value)
    {
        return 0;
    }
}

public struct Fixed(int value) : IConstraint
{
    public int Value = value;
    
    public int Calculate(int value)
    {
        return Value;
    }
}

public struct Relative(float value) : IConstraint
{
    public float Value = value;
    
    public int Calculate(int value)
    {
        return (int)(Value * value);
    }
}

public enum Anchor
{
    TopLeft,
    TopCenter,
    TopRight,
    CenterRight,
    BottomRight,
    BottomCenter,
    BottomLeft,
    CenterLeft,
    Center,
}