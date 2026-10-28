using System;
using Swordfish.Settings;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Applies and detects client shadow quality presets.</summary>
internal static class ShadowQualityExtensions
{
    /// <summary>Applies a shadow quality preset to render settings.</summary>
    public static void Apply(this ShadowQuality quality, in RenderSettings renderSettings)
    {
        switch (quality)
        {
            case ShadowQuality.Off:
                renderSettings.Shadows.Enabled.Set(false);
                break;
            case ShadowQuality.Low:
                renderSettings.Shadows.Enabled.Set(true);
                renderSettings.Shadows.FilterMode.Set(ShadowFilterMode.Sharp);
                renderSettings.Shadows.Resolution.Set(512);
                renderSettings.Shadows.MaxDistance.Set(48f);
                renderSettings.Shadows.CascadeCount.Set(2);
                renderSettings.Shadows.MaxPointShadows.Set(2);
                break;
            case ShadowQuality.Medium:
                renderSettings.Shadows.Enabled.Set(true);
                renderSettings.Shadows.FilterMode.Set(ShadowFilterMode.Soft);
                renderSettings.Shadows.Resolution.Set(1024);
                renderSettings.Shadows.MaxDistance.Set(96f);
                renderSettings.Shadows.CascadeCount.Set(3);
                renderSettings.Shadows.MaxPointShadows.Set(3);
                break;
            case ShadowQuality.High:
                renderSettings.Shadows.Enabled.Set(true);
                renderSettings.Shadows.FilterMode.Set(ShadowFilterMode.Soft);
                renderSettings.Shadows.Resolution.Set(2048);
                renderSettings.Shadows.MaxDistance.Set(160f);
                renderSettings.Shadows.CascadeCount.Set(4);
                renderSettings.Shadows.MaxPointShadows.Set(4);
                break;
            case ShadowQuality.Ultra:
                renderSettings.Shadows.Enabled.Set(true);
                renderSettings.Shadows.FilterMode.Set(ShadowFilterMode.Soft);
                renderSettings.Shadows.Resolution.Set(4096);
                renderSettings.Shadows.MaxDistance.Set(256f);
                renderSettings.Shadows.CascadeCount.Set(4);
                renderSettings.Shadows.MaxPointShadows.Set(8);
                break;
            case ShadowQuality.Custom:
                renderSettings.Shadows.Enabled.Set(true);
                break;
        }
    }

    /// <summary>Detects the closest matching shadow quality preset from current settings.</summary>
    public static ShadowQuality FromSettings(in ShadowSettings shadows)
    {
        if (!shadows.Enabled.Get())
        {
            return ShadowQuality.Off;
        }

        if (shadows.Resolution.Get() == 512
            && shadows.CascadeCount.Get() == 2
            && shadows.MaxPointShadows.Get() == 2
            && shadows.FilterMode.Get() == ShadowFilterMode.Sharp
            && MathF.Abs(shadows.MaxDistance.Get() - 48f) < 1f
        ) {
            return ShadowQuality.Low;
        }

        if (shadows.Resolution.Get() == 1024
            && shadows.CascadeCount.Get() == 3
            && shadows.MaxPointShadows.Get() == 3
            && shadows.FilterMode.Get() == ShadowFilterMode.Soft
            && MathF.Abs(shadows.MaxDistance.Get() - 96f) < 1f
        ) {
            return ShadowQuality.Medium;
        }

        if (shadows.Resolution.Get() == 2048
            && shadows.CascadeCount.Get() == 4
            && shadows.MaxPointShadows.Get() == 4
            && shadows.FilterMode.Get() == ShadowFilterMode.Soft
            && MathF.Abs(shadows.MaxDistance.Get() - 160f) < 1f
        ) {
            return ShadowQuality.High;
        }

        if (shadows.Resolution.Get() == 4096
            && shadows.CascadeCount.Get() == 4
            && shadows.MaxPointShadows.Get() == 8
            && shadows.FilterMode.Get() == ShadowFilterMode.Soft
            && MathF.Abs(shadows.MaxDistance.Get() - 256f) < 1f
        ) {
            return ShadowQuality.Ultra;
        }

        return ShadowQuality.Custom;
    }
}