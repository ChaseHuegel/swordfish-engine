using System.Numerics;

namespace Reef.Text;

public readonly struct GlyphLayout(IntRect bbox, IntRect uv, Vector4? color = null)
{
    public readonly IntRect BBOX = bbox;
    public readonly IntRect UV = uv;
    public readonly Vector4? Color = color;

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