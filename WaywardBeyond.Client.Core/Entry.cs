using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using LibNoise.Primitive;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.Bricks;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Bricks;

namespace WaywardBeyond.Client.Core;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class Entry(in ILogger logger, in IFileParseService fileParseService, in IECSContext ecsContext, in IPhysics physics) : IEntryPoint, IAutoActivate
{
    private readonly ILogger _logger = logger;
    private readonly IFileParseService _fileParseService = fileParseService;
    private readonly IECSContext _ecsContext = ecsContext;
    private readonly IPhysics _physics = physics;
    
    public void Run()
    {
        _physics.SetGravity(Vector3.Zero);
        
        var grid = _fileParseService.Parse<BrickGrid>(AssetPaths.Root.At("saves").At("mainMenuVoxObj.svo"));
        var shader = _fileParseService.Parse<Shader>(AssetPaths.Shaders.At("lightedArray.glsl"));
        var textureArray = _fileParseService.Parse<TextureArray>(AssetPaths.Textures.At("block\\"));

        var brickEntityBuilder = new BrickEntityBuilder(shader, textureArray, _fileParseService, _ecsContext.World.DataStore);
        
        Task.Run(LoadWorld);
        Task? LoadWorld()
        {
            byte[] seedBytes = "Wayward Beyond"u8.ToArray();
            byte[] seedHash = SHA1.HashData(seedBytes);
            var seed = BitConverter.ToInt32(seedHash);
            
            const int asteroidCount = 20;
            const int asteroidMinSize = 20;
            const int asteroidMaxSize = 150;
            
            const int worldHeight = 100;
            const int worldSpan = 300;
            
            _logger.LogInformation("Loading world...");
        
            brickEntityBuilder.Create("Wayward Ship", grid, Vector3.Zero, Quaternion.Identity, Vector3.One);

            var randomizer = new Randomizer(seed);
            for (var i = 0; i < asteroidCount; i++)
            {
                CreateAsteroid(randomizer, brickEntityBuilder, randomizer.NextInt(-worldSpan, worldSpan), randomizer.NextInt(-worldHeight, worldHeight), randomizer.NextInt(-worldSpan, worldSpan), randomizer.NextInt(asteroidMinSize, asteroidMaxSize));
                _logger.LogInformation("Created asteroid {num}.", i);
            }

            _logger.LogInformation("Done loading world.");
            return Task.CompletedTask;
        }
    }

    private static void CreateAsteroid(Randomizer randomizer, BrickEntityBuilder brickEntityBuilder, int originX, int originY, int originZ, int diameter)
    {
        var asteroidGrid = new BrickGrid(16);
        var rockBrick = new Brick(1)
        {
            Name = "rock",
        };
        
        var simplex = new SimplexPerlin();
        
        int width = diameter / 2;
        int centerOfMass = diameter / 2;
        int radius = randomizer.NextInt(width / 5, width);
        var origin = new Vector3(centerOfMass);
        int offset = randomizer.NextInt(1000);
        float frequency = randomizer.NextFloat() * 0.03f + 0.02f;
        float amplitude = randomizer.NextFloat() * 0.5f + 0.2f;
        
        for (var x = 0; x < diameter; x++)
        for (var y = 0; y < diameter; y++)
        for (var z = 0; z < diameter; z++)
        {
            var pos = new Vector3(x, y, z);
            float distance = Vector3.Distance(pos, origin);

            float percentDistance = distance / radius;
            
            float value = GetLayeredNoise(simplex, 2, frequency, amplitude, x + offset * diameter, y, z);

            bool solid = percentDistance < value;
            if (!solid)
            {
                continue;
            }
            
            asteroidGrid.Set(x, y, z, rockBrick);
        }

        var position = new Vector3(originX, originY, originZ);
        
        float yaw = randomizer.NextFloat() * 360f * MathS.DEGREES_TO_RADIANS;
        float pitch = randomizer.NextFloat() * 360f * MathS.DEGREES_TO_RADIANS;
        float roll = randomizer.NextFloat() * 360f * MathS.DEGREES_TO_RADIANS;
        var orientation = Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll);
        
        brickEntityBuilder.Create("asteroid", asteroidGrid, position, orientation, Vector3.One);

        float GetLayeredNoise(SimplexPerlin simplexPerlin, int octaves, float frequency, float amplitude, int x, int y, int z)
        {
            var value = 0f;
            for (var octave = 0; octave < octaves; octave++)
            {
                value += simplexPerlin.GetValue(x * frequency, y * frequency, z * frequency) * amplitude;
                frequency *= 2;
                amplitude /= 2;
            }
            
            value /= octaves;
            float normalizedValue = (value + 1f) / 2f;
            return normalizedValue;
        }
    }
}