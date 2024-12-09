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
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Generation;

namespace WaywardBeyond.Client.Core;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class Entry(in IFileParseService fileParseService, in IECSContext ecsContext, in IPhysics physics, in IShortcutService shortcutService, in IWindowContext windowContext)
    : IEntryPoint, IAutoActivate
{
    private readonly IFileParseService _fileParseService = fileParseService;
    private readonly IECSContext _ecsContext = ecsContext;
    private readonly IPhysics _physics = physics;
    private readonly IShortcutService _shortcutService = shortcutService;
    private readonly IWindowContext _windowContext = windowContext;
    
    public void Run()
    {
        Shortcut quitShortcut = new(
            "Quit Game",
            "General",
            ShortcutModifiers.None,
            Key.Esc,
            Shortcut.DefaultEnabled,
            _windowContext.Close
        );
        _shortcutService.RegisterShortcut(quitShortcut);
        
        _physics.SetGravity(Vector3.Zero);
        
        var shader = _fileParseService.Parse<Shader>(AssetPaths.Shaders.At("lightedArray.glsl"));
        var textureArray = _fileParseService.Parse<TextureArray>(AssetPaths.Textures.At("block\\"));

        var brickEntityBuilder = new BrickEntityBuilder(shader, textureArray, _fileParseService, _ecsContext.World.DataStore);
        var worldGenerator = new WorldGenerator("wayward beyond", brickEntityBuilder);
        
        Task.Run(worldGenerator.Generate);
        
        Entity player = _ecsContext.World.NewEntity();
        player.AddOrUpdate(new IdentifierComponent("Player", "player"));
        player.AddOrUpdate(new TransformComponent(Vector3.Zero, Quaternion.Identity));
        player.Add<PlayerComponent>();
    }
}