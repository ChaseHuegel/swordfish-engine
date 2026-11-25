using System;
using System.Numerics;

namespace WaywardBeyond.Bricks;

/// <summary>Converts normalized sRGB color components to linear RGB for PBR shading.</summary>
internal static class Srgb
{
    /// <summary>Converts one normalized sRGB component (0..1) to linear RGB.</summary>
    public static float ToLinear(float srgb)
    {
        return srgb <= 0.04045f ? srgb / 12.92f : MathF.Pow((srgb + 0.055f) / 1.055f, 2.4f);
    }

    /// <summary>Converts normalized sRGB components (0..1) to linear RGB.</summary>
    public static Vector3 ToLinear(in Vector3 srgb)
    {
        return new Vector3(ToLinear(srgb.X), ToLinear(srgb.Y), ToLinear(srgb.Z));
    }
}
