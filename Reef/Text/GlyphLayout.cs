using System.Numerics;

namespace Reef.Text;

/// <summary>
///     Bounding box, atlas UV coordinates, and color of a laid-out glyph.
/// </summary>
public readonly struct GlyphLayout(IntRect bbox, IntRect uv, Vector4? color = null, bool isWhitespace = false)
{
    public readonly IntRect BBOX = bbox;
    public readonly IntRect UV = uv;
    public readonly Vector4? Color = color;
    public readonly bool IsWhitespace = isWhitespace;

    /// <summary>
    ///     Resolves the color to draw with. The element color is used when no run
    ///     color is set, otherwise the run color's alpha is mixed with the element color's alpha.
    /// </summary>
    public Vector4 ResolveColor(Vector4 elementColor)
    {
        if (Color == null)
        {
            return elementColor;
        }

        Vector4 baseColor = Color.Value;
        return new Vector4(baseColor.X, baseColor.Y, baseColor.Z, baseColor.W * elementColor.W);
    }
}
