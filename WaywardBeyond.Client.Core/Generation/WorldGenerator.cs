using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Generation.Structures;

namespace WaywardBeyond.Client.Core.Generation;

internal sealed class WorldGenerator
{
    private readonly AsteroidGenerator _asteroidGenerator;
    private readonly Randomizer _randomizer;

    public WorldGenerator(in string seed, in BrickEntityBuilder brickEntityBuilder)
    {
        byte[] seedBytes = Encoding.UTF8.GetBytes(seed);
        byte[] seedHash = SHA1.HashData(seedBytes);
        var seedValue = BitConverter.ToInt32(seedHash);
        _randomizer = new Randomizer(seedValue);

        _asteroidGenerator = new AsteroidGenerator(seedValue, brickEntityBuilder);
    }
    
    public Task Generate()
    {
        const int asteroidCount = 20;
        const int asteroidMinSize = 20;
        const int asteroidMaxSize = 150;

        const int worldHeight = 100;
        const int worldSpan = 300;
        
        for (var i = 0; i < asteroidCount; i++)
        {
            var position = new Vector3
            (
                _randomizer.NextInt(-worldSpan, worldSpan),
                _randomizer.NextInt(-worldHeight, worldHeight),
                _randomizer.NextInt(-worldSpan, worldSpan)
            );
                
            _asteroidGenerator.GenerateAt(position, _randomizer.NextInt(asteroidMinSize, asteroidMaxSize));
        }

        return Task.CompletedTask;
    }
}