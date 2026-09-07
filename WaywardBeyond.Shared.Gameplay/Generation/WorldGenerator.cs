using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Gameplay.Generation.Structures;
using WaywardBeyond.Shared.Gameplay.Generation.Noise;

namespace WaywardBeyond.Shared.Gameplay;

using AsteroidStructure = (Vector3 Position, int Radius);

/// <summary>
///     Deterministic world generation: produces the fixed set of asteroid structures for a seed as
///     serialized <see cref="GeneratedVoxelEntity"/> data. Rendered/server-agnostic - it neither constructs
///     ECS entities nor meshes. The server runs it to author a new world and every consumer
///     (<see cref="WaywardBeyond.Server.Core"/>, tests, headless validation) derives identical data from the
///     same seed.
/// </summary>
public sealed class WorldGenerator
{
    private readonly int _seed;
    private readonly AsteroidGenerator _asteroidGenerator;
    private readonly Randomizer _randomizer;

    public WorldGenerator(in int seed)
    {
        _seed = seed;
        _asteroidGenerator = new AsteroidGenerator(seed, WorldMaterialCatalog.Rock, WorldMaterialCatalog.Ice);
        _randomizer = new Randomizer(seed);
    }

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

    public GeneratedVoxelEntity[] Generate()
    {
        const int asteroidCount = 20;
        const int asteroidMinRadius = 10;
        const int asteroidMaxRadius = 75;

        const int worldHeight = 100;
        const int worldSpan = 300;

        var randomizer = _randomizer;

        var asteroids = new List<AsteroidStructure>(asteroidCount);
        while (asteroids.Count < asteroidCount)
        {
            var position = new Vector3
            (
                randomizer.NextInt(-worldSpan, worldSpan),
                randomizer.NextInt(-worldHeight, worldHeight),
                randomizer.NextInt(-worldSpan, worldSpan)
            );

            int radius = randomizer.NextInt(asteroidMinRadius, asteroidMaxRadius);

            if (asteroids.Any(asteroid => Intersection.SphereToSphere(asteroid.Position, asteroid.Radius, position, radius)))
            {
                continue;
            }

            asteroids.Add(new AsteroidStructure(position, radius));
        }

        var generated = new GeneratedVoxelEntity[asteroids.Count];
        for (var i = 0; i < asteroids.Count; i++)
        {
            AsteroidStructure asteroid = asteroids[i];
            generated[i] = _asteroidGenerator.GenerateAt(asteroid.Position, diameter: asteroid.Radius * 2);
        }

        return generated;
    }
}