using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Generation.Structures;
using WaywardBeyond.Client.Core.Voxels.Building;

namespace WaywardBeyond.Client.Core.Generation;

using AsteroidStructure = (Vector3 Position, int Radius);

internal sealed class WorldGenerator
{
    private readonly AsteroidGenerator _asteroidGenerator;
    private readonly Randomizer _randomizer;

    public WorldGenerator(in string seed, in VoxelEntityBuilder voxelEntityBuilder, in IAssetDatabase<BrickInfo> brickDatabase)
    {
        byte[] seedBytes = Encoding.UTF8.GetBytes(seed);
        byte[] seedHash = SHA1.HashData(seedBytes);
        var seedValue = BitConverter.ToInt32(seedHash);
        _randomizer = new Randomizer(seedValue);

        _asteroidGenerator = new AsteroidGenerator(seedValue, voxelEntityBuilder, brickDatabase);
    }
    
    public Task Generate()
    {
        const int asteroidCount = 20;
        const int asteroidMinRadius = 10;
        const int asteroidMaxRadius = 75;

        const int worldHeight = 100;
        const int worldSpan = 300;

        var asteroids = new List<AsteroidStructure>(asteroidCount);
        while (asteroids.Count < asteroidCount)
        {
            var position = new Vector3
            (
                _randomizer.NextInt(-worldSpan, worldSpan),
                _randomizer.NextInt(-worldHeight, worldHeight),
                _randomizer.NextInt(-worldSpan, worldSpan)
            );

            int radius = _randomizer.NextInt(asteroidMinRadius, asteroidMaxRadius);

            if (asteroids.Any(asteroid => Intersection.SphereToSphere(asteroid.Position, asteroid.Radius, position, radius)))
            {
                continue;
            }
            
            asteroids.Add(new AsteroidStructure(position, radius));
        }
        
        foreach (AsteroidStructure asteroid in asteroids)
        {
            _asteroidGenerator.GenerateAt(asteroid.Position, diameter: asteroid.Radius * 2);
        }

        return Task.CompletedTask;
    }
}