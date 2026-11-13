using System;
using System.Numerics;

namespace Swordfish.Graphics;

/// <summary>Transfer functions and tonemapping for linear and sRGB color values.</summary>
public static class ColorTransferMath
{
    /// <summary>Decodes an sRGB channel in [0, 1] to linear light.</summary>
    public static float SrgbToLinear(float srgb)
    {
        srgb = Math.Clamp(srgb, 0f, 1f);
        return srgb <= 0.04045f ? srgb / 12.92f : MathF.Pow((srgb + 0.055f) / 1.055f, 2.4f);
    }

    /// <summary>Encodes a linear channel in [0, 1] to sRGB.</summary>
    public static float LinearToSrgb(float linear)
    {
        linear = Math.Clamp(linear, 0f, 1f);
        return linear < 0.0031308f ? linear * 12.92f : 1.055f * MathF.Pow(linear, 1.0f / 2.4f) - 0.055f;
    }

    /// <summary>Decodes an sRGB color in [0, 1] to linear light.</summary>
    public static Vector3 SrgbToLinear(Vector3 srgb)
    {
        return new Vector3(
            SrgbToLinear(srgb.X),
            SrgbToLinear(srgb.Y),
            SrgbToLinear(srgb.Z)
        );
    }

    /// <summary>Encodes a linear color in [0, 1] to sRGB.</summary>
    public static Vector3 LinearToSrgb(Vector3 linear)
    {
        return new Vector3(
            LinearToSrgb(linear.X),
            LinearToSrgb(linear.Y),
            LinearToSrgb(linear.Z)
        );
    }

    /// <summary>Maps a non-negative linear HDR color into [0, 1] with Khronos PBR Neutral.</summary>
    public static Vector3 TonemapPbrNeutral(Vector3 color)
    {
        const float START_COMPRESSION = 0.8f - 0.04f;
        const float DESATURATION = 0.15f;

        float x = MathF.Min(color.X, MathF.Min(color.Y, color.Z));
        float offset = x < 0.08f ? x - 6.25f * x * x : 0.04f;
        color -= new Vector3(offset);

        float peak = MathF.Max(color.X, MathF.Max(color.Y, color.Z));
        if (peak < START_COMPRESSION)
        {
            return color;
        }

        const float D = 1.0f - START_COMPRESSION;
        float newPeak = 1.0f - (D * D) / (peak + D - START_COMPRESSION);
        color *= newPeak / peak;

        float g = 1.0f - 1.0f / (DESATURATION * (peak - newPeak) + 1.0f);
        return Vector3.Lerp(color, new Vector3(newPeak), g);
    }
}
