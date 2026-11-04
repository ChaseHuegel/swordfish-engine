using System;
using Swordfish.Settings;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Applies and detects client ambient occlusion quality presets.</summary>
internal static class AoQualityExtensions
{
    /// <summary>Applies an ambient occlusion quality preset to render settings.</summary>
    public static void Apply(this AoQuality quality, in RenderSettings renderSettings)
    {
        switch (quality)
        {
            case AoQuality.Low:
                renderSettings.SAO.Samples.Set(4);
                renderSettings.SAO.ResolutionScale.Set(0.25f);
                renderSettings.SAO.MaxDistance.Set(32f);
                break;
            case AoQuality.Medium:
                renderSettings.SAO.Samples.Set(8);
                renderSettings.SAO.ResolutionScale.Set(0.5f);
                renderSettings.SAO.MaxDistance.Set(48f);
                break;
            case AoQuality.High:
                renderSettings.SAO.Samples.Set(16);
                renderSettings.SAO.ResolutionScale.Set(0.5f);
                renderSettings.SAO.MaxDistance.Set(64f);
                break;
            case AoQuality.Custom:
                //  Hand tuned values stay as they are.
                break;
        }
    }

    /// <summary>Detects the closest matching ambient occlusion preset from current settings.</summary>
    public static AoQuality FromSettings(in SAOSettings sao)
    {
        int samples = sao.Samples.Get();
        float scale = sao.ResolutionScale.Get();
        float distance = sao.MaxDistance.Get();

        if (Matches(samples, 4, scale, 0.25f, distance, 32f))
        {
            return AoQuality.Low;
        }

        if (Matches(samples, 8, scale, 0.5f, distance, 48f))
        {
            return AoQuality.Medium;
        }

        if (Matches(samples, 16, scale, 0.5f, distance, 64f))
        {
            return AoQuality.High;
        }

        return AoQuality.Custom;
    }

    private static bool Matches(int samples, int expectedSamples, float scale, float expectedScale, float distance, float expectedDistance)
    {
        return samples == expectedSamples
            && MathF.Abs(scale - expectedScale) < 0.01f
            && MathF.Abs(distance - expectedDistance) < 0.5f;
    }
}
