using Swordfish.Library.Types;

namespace Swordfish.Settings;

/// <summary>Runtime settings for CPU particle simulation and GPU instanced rendering.</summary>
public sealed record ParticleSettings
{
    /// <summary>Enables particle simulation and rendering.</summary>
    public DataBinding<bool> Enabled { get; private set; } = new(true);

    /// <summary>Maximum number of concurrent live particles across all emitters and bursts.</summary>
    public DataBinding<int> MaxParticles { get; private set; } = new(4096);

    /// <summary>Multiplier applied to emitter emission rates and burst particle counts, clamped to 0..float.MaxValue.</summary>
    public DataBinding<float> Density { get; private set; } = new(1f);
}
