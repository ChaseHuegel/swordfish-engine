using System;
using System.Numerics;

namespace Reef;

/// <summary>
///     A signed distance field font atlas. The per-texel distance is resolved
///     once at construction so glyph rasterization only reads a single float.
/// </summary>
public readonly struct PixelFontSDF
{
    public readonly PixelTexture Texture;
    public readonly float Fwidth;
    internal readonly float InvFwidth;
    internal readonly float[] Distances;

    public PixelFontSDF(PixelTexture texture, float fwidth)
    {
        Texture = texture;
        Fwidth = fwidth;
        InvFwidth = fwidth != 0f ? 1f / fwidth : 0f;
        Distances = new float[texture.Width * texture.Height];

        ReadOnlySpan<Vector4> pixels = texture.AsSpan();
        for (var i = 0; i < pixels.Length; i++)
        {
            Vector4 s = pixels[i];
            Distances[i] = Median(s.X, s.Y, s.Z) - 0.5f;
        }
    }

    private static float Median(float a, float b, float c)
    {
        return Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), c));
    }
}
