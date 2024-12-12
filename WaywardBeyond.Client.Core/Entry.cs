using System;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.IO;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Generation;
using WaywardBeyond.Client.Core.Generation.Structures;

namespace WaywardBeyond.Client.Core;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class Entry : IEntryPoint, IAutoActivate
{
    private readonly IFileParseService _fileParseService;
    private readonly IECSContext _ecsContext;
    private readonly IPhysics _physics;
    private readonly IShortcutService _shortcutService;
    private readonly IWindowContext _windowContext;


    public Entry(in IFileParseService fileParseService, in IECSContext ecsContext, in IPhysics physics, in IShortcutService shortcutService, in IWindowContext windowContext)
    {
        _fileParseService = fileParseService;
        _ecsContext = ecsContext;
        _physics = physics;
        _shortcutService = shortcutService;
        _windowContext = windowContext;
        
        _windowContext.SetTitle($"Wayward Beyond {WaywardBeyond.Version}");
    }

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
        
        // Task.Run(worldGenerator.Generate);
        
        var asteroidGenerator = new AsteroidGenerator(0, brickEntityBuilder);
        asteroidGenerator.GenerateAt(Vector3.Zero, 20);
        
        Entity player = _ecsContext.World.NewEntity();
        player.AddOrUpdate(new IdentifierComponent("Player", "player"));
        player.AddOrUpdate(new TransformComponent(Vector3.Zero, Quaternion.Identity));
        player.Add<PlayerComponent>();
    }
}