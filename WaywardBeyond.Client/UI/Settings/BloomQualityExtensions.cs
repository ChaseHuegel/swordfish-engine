using System;
using Swordfish.Settings;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Applies and detects client bloom quality presets.</summary>
internal static class BloomQualityExtensions
{
    /// <summary>Applies a bloom quality preset to render settings.</summary>
    public static void Apply(this BloomQuality quality, in RenderSettings renderSettings)
    {
        switch (quality)
        {
            case BloomQuality.Low:
                renderSettings.Bloom.Passes.Set(2);
                renderSettings.Bloom.Intensity.Set(1f);
                break;
            case BloomQuality.Medium:
                renderSettings.Bloom.Passes.Set(3);
                renderSettings.Bloom.Intensity.Set(0.75f);
                break;
            case BloomQuality.High:
                renderSettings.Bloom.Passes.Set(4);
                renderSettings.Bloom.Intensity.Set(0.65f);
                break;
            case BloomQuality.Custom:
                //  Hand tuned values stay as they are.
                break;
        }
    }

    /// <summary>Detects the closest matching bloom quality preset from current settings.</summary>
    public static BloomQuality FromSettings(in BloomSettings bloom)
    {
        int levels = bloom.Passes.Get();
        float intensity = bloom.Intensity.Get();

        if (Matches(levels, 2, intensity, 1f))
        {
            return BloomQuality.Low;
        }

        if (Matches(levels, 3, intensity, 0.75f))
        {
            return BloomQuality.Medium;
        }

        if (Matches(levels, 4, intensity, 0.65f))
        {
            return BloomQuality.High;
        }

        return BloomQuality.Custom;
    }

    private static bool Matches(int levels, int expectedLevels, float intensity, float expectedIntensity)
    {
        return levels == expectedLevels && MathF.Abs(intensity - expectedIntensity) < 0.05f;
    }
}
