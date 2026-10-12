using Swordfish.Library.Types;

namespace WaywardBeyond.Config;

/// <summary>Asteroid generator configuration.</summary>
public sealed record AsteroidConfig
{
    /// <summary>The number of simplex noise octaves per sample.</summary>
    public DataBinding<int> NoiseOctaves { get; private set; } = new(2);

    /// <summary>The base noise frequency; a random value up to <see cref="NoiseFrequencyRange"/> is added per world.</summary>
    public DataBinding<float> NoiseFrequencyMin { get; private set; } = new(0.02f);

    /// <summary>The per-world random noise-frequency range.</summary>
    public DataBinding<float> NoiseFrequencyRange { get; private set; } = new(0.03f);

    /// <summary>The base noise amplitude; a random value up to <see cref="NoiseAmplitudeRange"/> is added per world.</summary>
    public DataBinding<float> NoiseAmplitudeMin { get; private set; } = new(0.2f);

    /// <summary>The per-world random noise-amplitude range.</summary>
    public DataBinding<float> NoiseAmplitudeRange { get; private set; } = new(0.5f);

    /// <summary>The range of the per-world noise offset.</summary>
    public DataBinding<int> NoiseOffsetRange { get; private set; } = new(1000);

    /// <summary>Divides an asteroid's width to bound its random radius.</summary>
    public DataBinding<int> RadiusMinDivisor { get; private set; } = new(5);

    /// <summary>Brick names a generated asteroid may be built from.</summary>
    public DataBinding<string[]> Materials { get; private set; } = new(["wb:rock", "wb:ice"]);
}