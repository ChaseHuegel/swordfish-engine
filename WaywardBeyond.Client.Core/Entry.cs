using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.IO;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Generation;

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
        
        var shader = _fileParseService.Parse<Shader>(AssetPaths.Shaders.At("lightedArray.glsl"));
        var textureArray = _fileParseService.Parse<TextureArray>(AssetPaths.Textures.At("block\\"));

        var brickEntityBuilder = new BrickEntityBuilder(shader, textureArray, _fileParseService, _ecsContext.World.DataStore);
        var worldGenerator = new WorldGenerator("wayward beyond", brickEntityBuilder);
        
        Task.Run(worldGenerator.Generate);
    }
}