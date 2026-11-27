using System;
using Swordfish.Library.Types;

namespace WaywardBeyond.Config;

/// <summary>Adventure starter wreckage generation configuration.</summary>
public sealed record StarterWreckConfig
{
    /// <summary>The bundled blueprint identifier spawned near the player in new Adventure levels.</summary>
    public DataBinding<string> BlueprintId { get; private set; } = new("builtin/wrecked_lifeboat");

    /// <summary>The minimum distance in meters from <c>LevelSpawn</c> to the starter wreck center.</summary>
    public DataBinding<float> SpawnDistanceMin { get; private set; } = new(14f);

    /// <summary>The maximum distance in meters from <c>LevelSpawn</c> to the starter wreck center.</summary>
    public DataBinding<float> SpawnDistanceMax { get; private set; } = new(20f);

    /// <summary>The minimum clearance radius in meters kept free of asteroids around <c>LevelSpawn</c>.</summary>
    public DataBinding<float> SpawnClearanceRadius { get; private set; } = new(12f);

    /// <summary>Extra clearance radius in meters added beyond the wreck bounding radius when placing asteroids.</summary>
    public DataBinding<float> AsteroidClearancePadding { get; private set; } = new(12f);

    /// <summary>Throws when the starter wreck tuning is invalid.</summary>
    public void Validate()
    {
        if (SpawnDistanceMin.Get() < 0f)
        {
            throw new InvalidOperationException("LevelGenerator.StarterWreck.SpawnDistanceMin must not be negative.");
        }

        if (SpawnDistanceMax.Get() < SpawnDistanceMin.Get())
        {
            throw new InvalidOperationException(
                "LevelGenerator.StarterWreck.SpawnDistanceMax must be at least SpawnDistanceMin.");
        }

        if (SpawnClearanceRadius.Get() < 0f)
        {
            throw new InvalidOperationException("LevelGenerator.StarterWreck.SpawnClearanceRadius must not be negative.");
        }

        if (AsteroidClearancePadding.Get() < 0f)
        {
            throw new InvalidOperationException("LevelGenerator.StarterWreck.AsteroidClearancePadding must not be negative.");
        }
    }
}
