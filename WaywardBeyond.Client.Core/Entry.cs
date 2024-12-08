using System;
using System.Numerics;
using System.Security.Cryptography;
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
using WaywardBeyond.Client.Core.Generation.Structures;

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
        
        byte[] seedBytes = "Wayward Beyond"u8.ToArray();
        byte[] seedHash = SHA1.HashData(seedBytes);
        var seed = BitConverter.ToInt32(seedHash);
        var randomizer = new Randomizer(seed);

        var asteroidGenerator = new AsteroidGenerator(seed, brickEntityBuilder);
        
        Task.Run(LoadWorld);
        Task? LoadWorld()
        {
            const int asteroidCount = 20;
            const int asteroidMinSize = 20;
            const int asteroidMaxSize = 150;
            
            const int worldHeight = 100;
            const int worldSpan = 300;
            
            _logger.LogInformation("Loading world...");
        
            brickEntityBuilder.Create("Wayward Ship", grid, Vector3.Zero, Quaternion.Identity, Vector3.One);

            for (var i = 0; i < asteroidCount; i++)
            {
                var position = new Vector3
                (
                    randomizer.NextInt(-worldSpan, worldSpan),
                    randomizer.NextInt(-worldHeight, worldHeight),
                    randomizer.NextInt(-worldSpan, worldSpan)
                );
                
                asteroidGenerator.GenerateAt(position, randomizer.NextInt(asteroidMinSize, asteroidMaxSize));
                _logger.LogInformation("Created asteroid {num}.", i);
            }

            _logger.LogInformation("Done loading world.");
            return Task.CompletedTask;
        }
    }
}