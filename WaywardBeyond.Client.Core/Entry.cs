using System.Numerics;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.Bricks;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.IO;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Bricks;

namespace WaywardBeyond.Client.Core;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class Entry(in IFileParseService fileParseService, in IECSContext ecsContext, in IPhysics physics) : IEntryPoint, IAutoActivate
{
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

        Entity ship = brickEntityBuilder.Create("Wayward Ship", grid, Vector3.Zero, Quaternion.Identity, Vector3.One);
    }
}