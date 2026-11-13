using System.Numerics;

namespace Swordfish.Graphics;

/// <summary>Transfer functions for decoding sRGB color values to linear light.</summary>
public static class Tonemapper
{
    /// <summary>Decodes an sRGB channel in [0, 1] to linear light.</summary>
    public static float SrgbToLinear(float srgb)
    {
        srgb = Math.Clamp(srgb, 0f, 1f);
        return srgb <= 0.04045f ? srgb / 12.92f : MathF.Pow((srgb + 0.055f) / 1.055f, 2.4f);
    }

    /// <summary>Decodes an sRGB color in [0, 1] to linear light.</summary>
    public static Vector3 SrgbToLinear(in Vector3 srgb)
    {
        return new Vector3(
            SrgbToLinear(srgb.X),
            SrgbToLinear(srgb.Y),
            SrgbToLinear(srgb.Z)
        );
    }
}
