using System.Numerics;

namespace Reef.UI;

/// <summary>Describes a border drawn inside an element's bounds.</summary>
public struct BorderStyle
{
    /// <summary>Border thickness in pixels. Zero disables the border.</summary>
    public int Thickness;

    /// <summary>Border color, including alpha.</summary>
    public Vector4 Color;
}
