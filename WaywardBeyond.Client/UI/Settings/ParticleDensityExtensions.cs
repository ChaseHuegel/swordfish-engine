using System;
using Swordfish.Settings;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Applies and detects client particle density presets.</summary>
internal static class ParticleDensityExtensions
{
    /// <summary>Applies a particle density preset to render settings.</summary>
    public static void Apply(this ParticleDensity density, in RenderSettings renderSettings)
    {
        switch (density)
        {
            case ParticleDensity.Off:
                renderSettings.Particles.Enabled.Set(false);
                break;
            case ParticleDensity.Low:
                renderSettings.Particles.Enabled.Set(true);
                renderSettings.Particles.Density.Set(0.25f);
                break;
            case ParticleDensity.Medium:
                renderSettings.Particles.Enabled.Set(true);
                renderSettings.Particles.Density.Set(0.5f);
                break;
            case ParticleDensity.High:
                renderSettings.Particles.Enabled.Set(true);
                renderSettings.Particles.Density.Set(0.75f);
                break;
            case ParticleDensity.Ultra:
                renderSettings.Particles.Enabled.Set(true);
                renderSettings.Particles.Density.Set(1f);
                break;
            case ParticleDensity.Custom:
                renderSettings.Particles.Enabled.Set(true);
                break;
        }
    }

    /// <summary>Detects the closest matching particle density preset from current settings.</summary>
    public static ParticleDensity FromSettings(in ParticleSettings particles)
    {
        if (!particles.Enabled.Get())
        {
            return ParticleDensity.Off;
        }

        float density = particles.Density.Get();
        if (MathF.Abs(density - 0.25f) < 0.01f)
        {
            return ParticleDensity.Low;
        }

        if (MathF.Abs(density - 0.5f) < 0.01f)
        {
            return ParticleDensity.Medium;
        }

        if (MathF.Abs(density - 0.75f) < 0.01f)
        {
            return ParticleDensity.High;
        }

        if (MathF.Abs(density - 1f) < 0.01f)
        {
            return ParticleDensity.Ultra;
        }

        return ParticleDensity.Custom;
    }
}
