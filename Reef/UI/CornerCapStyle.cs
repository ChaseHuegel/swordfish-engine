using System.Numerics;

namespace Reef.UI;

/// <summary>Describes L-shaped corner caps drawn at an element's corners.</summary>
public struct CornerCapStyle
{
    /// <summary>Length of each cap arm in pixels. Zero disables the caps.</summary>
    public int Length;

    /// <summary>Thickness of each cap arm in pixels.</summary>
    public int Thickness;

    /// <summary>Cap color, including alpha.</summary>
    public Vector4 Color;
}
