using System.Numerics;

namespace Reef.UI;

/// <summary>Describes a border drawn inside an element's bounds.</summary>
public struct BorderStyle
{
    /// <summary>Border thickness in pixels. Zero disables the border.</summary>
    public int Thickness;

    /// <summary>Border color, including alpha.</summary>
    public Vector4 Color;

    /// <summary>Top border thickness. Zero uses <see cref="Thickness"/>.</summary>
    public int TopThickness;

    /// <summary>Right border thickness. Zero uses <see cref="Thickness"/>.</summary>
    public int RightThickness;

    /// <summary>Bottom border thickness. Zero uses <see cref="Thickness"/>.</summary>
    public int BottomThickness;

    /// <summary>Left border thickness. Zero uses <see cref="Thickness"/>.</summary>
    public int LeftThickness;

    /// <summary>Top border color. Alpha zero uses <see cref="Color"/>.</summary>
    public Vector4 TopColor;

    /// <summary>Right border color. Alpha zero uses <see cref="Color"/>.</summary>
    public Vector4 RightColor;

    /// <summary>Bottom border color. Alpha zero uses <see cref="Color"/>.</summary>
    public Vector4 BottomColor;

    /// <summary>Left border color. Alpha zero uses <see cref="Color"/>.</summary>
    public Vector4 LeftColor;
}
