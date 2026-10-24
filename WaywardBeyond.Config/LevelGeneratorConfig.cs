using System;
using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace WaywardBeyond.Config;

/// <summary>Level generation configuration.</summary>
public sealed class LevelGeneratorConfig : Config<LevelGeneratorConfig>
{
    /// <summary>The number of asteroid structures to generate per level.</summary>
    public DataBinding<int> AsteroidCount { get; private set; } = new(20);

    /// <summary>The minimum asteroid radius.</summary>
    public DataBinding<int> AsteroidMinRadius { get; private set; } = new(10);

    /// <summary>The maximum asteroid radius.</summary>
    public DataBinding<int> AsteroidMaxRadius { get; private set; } = new(75);

    /// <summary>The world half-span along the Y axis.</summary>
    public DataBinding<int> WorldHeight { get; private set; } = new(100);

    /// <summary>The world half-span along the X and Z axes.</summary>
    public DataBinding<int> WorldSpan { get; private set; } = new(300);

    /// <summary>Asteroid generator configuration.</summary>
    public AsteroidConfig Asteroid { get; private set; } = new();

    /// <summary>Throws when the generation tuning cannot produce a level.</summary>
    public void Validate()
    {
        if (AsteroidCount.Get() < 1)
        {
            throw new InvalidOperationException("LevelGenerator.AsteroidCount must be at least 1.");
        }

        if (AsteroidMinRadius.Get() < 1)
        {
            throw new InvalidOperationException("LevelGenerator.AsteroidMinRadius must be at least 1.");
        }

        if (AsteroidMaxRadius.Get() < AsteroidMinRadius.Get())
        {
            throw new InvalidOperationException(
                "LevelGenerator.AsteroidMaxRadius must be at least AsteroidMinRadius.");
        }

        if (WorldHeight.Get() < 1)
        {
            throw new InvalidOperationException("LevelGenerator.WorldHeight must be at least 1.");
        }

        if (WorldSpan.Get() < 1)
        {
            throw new InvalidOperationException("LevelGenerator.WorldSpan must be at least 1.");
        }

        Asteroid.Validate();
    }
}