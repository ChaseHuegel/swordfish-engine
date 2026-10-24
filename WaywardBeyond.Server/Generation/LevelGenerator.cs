using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Config;
using WaywardBeyond.Data;

namespace WaywardBeyond.Server.Generation;

using AsteroidStructure = (Vector3 Position, int Radius);

/// <summary>
///     Deterministic level generation: produces the fixed set of asteroid structures for a seed as
///     serialized <see cref="VoxelEntityData"/>. Rendered/server-agnostic - it neither constructs ECS
///     entities nor meshes. The server runs it to author a new world, and the same seed derives
///     identical data, so a level can be regenerated.
/// </summary>
public sealed class LevelGenerator(in int seed, IBrickRegistry brickRegistry, LevelGeneratorConfig config)
{
    private readonly LevelGeneratorConfig _config = config;
    private readonly int _seed = seed;
    private readonly Randomizer _randomizer = new(seed);
    private readonly AsteroidGenerator _asteroidGenerator = new(brickRegistry, seed, config.Asteroid);

    //  Placement is rejection-sampled: a dense radius or a tight volume can make every candidate
    //  intersect, so cap the work instead of spinning on the server thread.
    private const int MAX_PLACEMENT_ATTEMPTS_PER_ASTEROID = 100;

    public static int HashSeed(string seed)
    {
        if (int.TryParse(seed, out int seedValue))
        {
            return seedValue;
        }

        byte[] seedBytes = Encoding.UTF8.GetBytes(seed);
        byte[] seedHash = SHA1.HashData(seedBytes);
        return BitConverter.ToInt32(seedHash);
    }

    public VoxelEntityData[] Generate()
    {
        int asteroidCount = _config.AsteroidCount;
        int asteroidMinRadius = _config.AsteroidMinRadius;
        int asteroidMaxRadius = _config.AsteroidMaxRadius;

        int worldHeight = _config.WorldHeight;
        int worldSpan = _config.WorldSpan;

        var asteroids = new List<AsteroidStructure>(asteroidCount);
        int attempts = 0;
        int maxAttempts = asteroidCount * MAX_PLACEMENT_ATTEMPTS_PER_ASTEROID;
        while (asteroids.Count < asteroidCount)
        {
            if (++attempts > maxAttempts)
            {
                throw new InvalidOperationException(
                    $"Could not place {asteroidCount} asteroids in the configured volume after {maxAttempts} attempts; " +
                    "reduce LevelGenerator.AsteroidCount or the asteroid radii, or widen the world span.");
            }

            var position = new Vector3
            (
                _randomizer.NextInt(-worldSpan, worldSpan),
                _randomizer.NextInt(-worldHeight, worldHeight),
                _randomizer.NextInt(-worldSpan, worldSpan)
            );

            int radius = _randomizer.NextInt(asteroidMinRadius, asteroidMaxRadius + 1);

            if (asteroids.Any(asteroid => Intersection.SphereToSphere(asteroid.Position, asteroid.Radius, position, radius)))
            {
                continue;
            }

            asteroids.Add(new AsteroidStructure(position, radius));
        }

        var generated = new VoxelEntityData[asteroids.Count];
        for (var i = 0; i < asteroids.Count; i++)
        {
            AsteroidStructure asteroid = asteroids[i];
            Uuid uuid = GenerateUuid(i);
            generated[i] = _asteroidGenerator.GenerateAt(asteroid.Position, diameter: asteroid.Radius * 2, uuid);
        }

        return generated;
    }

    /// <summary>
    ///     Derives a stable, non-zero structure uuid from the seed and the structure's index, so
    ///     regenerating a level produces the same identities.
    /// </summary>
    private Uuid GenerateUuid(int index)
    {
        return Uuid.FromValue(((ulong)(uint)_seed << 32) | (uint)(index + 1));
    }
}
