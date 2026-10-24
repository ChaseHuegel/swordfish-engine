using System;
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

    /// <summary>Throws when the noise or material tuning cannot produce a solid asteroid.</summary>
    public void Validate()
    {
        if (NoiseOctaves.Get() < 1)
        {
            throw new InvalidOperationException("LevelGenerator.Asteroid.NoiseOctaves must be at least 1.");
        }

        if (RadiusMinDivisor.Get() < 1)
        {
            throw new InvalidOperationException("LevelGenerator.Asteroid.RadiusMinDivisor must be at least 1.");
        }

        if (NoiseOffsetRange.Get() < 0)
        {
            throw new InvalidOperationException("LevelGenerator.Asteroid.NoiseOffsetRange must not be negative.");
        }

        if (NoiseFrequencyMin.Get() + NoiseFrequencyRange.Get() <= 0f)
        {
            throw new InvalidOperationException(
                "LevelGenerator.Asteroid noise frequency must be positive; the shape would be a constant sphere.");
        }

        if (NoiseAmplitudeMin.Get() + NoiseAmplitudeRange.Get() <= 0f)
        {
            throw new InvalidOperationException(
                "LevelGenerator.Asteroid noise amplitude must be positive; no voxel would be solid.");
        }

        if (Materials.Get().Length == 0)
        {
            throw new InvalidOperationException("LevelGenerator.Asteroid.Materials must name at least one brick.");
        }
    }
}