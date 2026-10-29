using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Reef.Generator;

internal enum MarkupProperty
{
    Color,
    BackgroundColor,
    TextColor,
    Padding,
    CornerRadius,
    BorderThickness,
    BorderColor,
    BorderTopThickness,
    BorderTopColor,
    BorderRightThickness,
    BorderRightColor,
    BorderBottomThickness,
    BorderBottomColor,
    BorderLeftThickness,
    BorderLeftColor,
    CornerCapsLength,
    CornerCapsThickness,
    CornerCapsColor,
    Overlay,
    FontSize,
    FontID,
    HorizontalAlignment,
    VerticalAlignment,
    NoWrap,
    Width,
    Height,
    X,
    Y,
    MinWidth,
    MinHeight,
    MaxWidth,
    MaxHeight,
    Anchors,
    Layout,
    Spacing,
}

internal enum MarkupConstraintKind
{
    Fixed,
    Relative,
    Fill,
    Fit,
    Shrink,
}

internal readonly struct MarkupConstraint
{
    public readonly MarkupConstraintKind Kind;
    public readonly int Fixed;
    public readonly float Relative;

    public MarkupConstraint(MarkupConstraintKind kind, int fixedValue, float relative)
    {
        Kind = kind;
        Fixed = fixedValue;
        Relative = relative;
    }
}

[System.Flags]
internal enum MarkupAnchors
{
    None = 0,
    Top = 1,
    Left = 2,
    Bottom = 4,
    Right = 8,
    Center = 16,
    Local = 32,
}

internal enum MarkupLayoutDirection
{
    Horizontal,
    Vertical,
    None,
}

internal enum MarkupHorizontalAlignment
{
    Left,
    Center,
    Right,
}

internal enum MarkupVerticalAlignment
{
    Top,
    Center,
    Bottom,
}

internal sealed class MarkupExpression
{
    public readonly string Raw;
    public readonly MarkupExpressionKind Kind;

    public MarkupExpression(string raw, MarkupExpressionKind kind)
    {
        Raw = raw;
        Kind = kind;
    }
}

internal enum MarkupExpressionKind
{
    Text,
    Color,
    Bool,
    Int,
    Constraint,
    FontId,
    Hotkey,
}

internal sealed class MarkupAssignment
{
    public MarkupProperty Property;
    public object Value = null!;
    public Location Location = Location.None;
    public bool IsDynamic;

    public MarkupAssignment(MarkupProperty property, object value, Location location)
    {
        Property = property;
        Value = value;
        Location = location;
        IsDynamic = value is MarkupExpression;
    }
}

internal sealed class MarkupStyle
{
    public readonly List<MarkupAssignment> Assignments = [];
}

internal sealed class MarkupValueList
{
    public readonly List<MarkupAssignment> Assignments = [];
    public readonly List<string> Errors = [];
}
