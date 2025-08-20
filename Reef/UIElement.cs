using System;
using System.Collections.Generic;
using System.Numerics;

namespace Reef;

public struct UIElement<TTextureData>
{
    public IntRect Rect;
    public Style Style;
    public Layout Layout;
    public Constraints Constraints;
    public List<UIElement<TTextureData>>? Children;
    public string? Text;
    public FontOptions FontOptions;
    public TTextureData? TextureData;
}

public struct Constraints
{
    public Anchors Anchors;
    
    public IConstraint? X;
    public IConstraint? Y;
    public IConstraint? Width;
    public IConstraint? Height;
    
    public int MinWidth;
    public int MinHeight;
}

public struct Style
{
    public Vector4 Color;
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
    None,
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

public struct Fill : IConstraint
{
    public int Calculate(int value)
    {
        return 0;
    }
}

public struct Shrink : IConstraint
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

[Flags]
public enum Anchors
{
    Top = 1,
    Left = 2,
    Bottom = 4,
    Right = 8,
    Center = 16,
}

public struct FontOptions
{
    public string? ID;
    public int Size;
}