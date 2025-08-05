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
    private readonly IECSContext _ecsContext;
    private readonly IPhysics _physics;
    private readonly IShortcutService _shortcutService;
    private readonly IWindowContext _windowContext;
    private readonly BrickEntityBuilder _brickEntityBuilder;

    public Entry(
        in IECSContext ecsContext,
        in IPhysics physics,
        in IShortcutService shortcutService,
        in IWindowContext windowContext,
        in BrickEntityBuilder brickEntityBuilder)
    {
        _ecsContext = ecsContext;
        _physics = physics;
        _shortcutService = shortcutService;
        _windowContext = windowContext;
        _brickEntityBuilder = brickEntityBuilder;
        
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

        var worldGenerator = new WorldGenerator("wayward beyond", _brickEntityBuilder);
        Task.Run(worldGenerator.Generate);
        
        var asteroidGenerator = new AsteroidGenerator(0, _brickEntityBuilder);
        asteroidGenerator.GenerateAt(Vector3.Zero, 20);
        
        Entity player = _ecsContext.World.NewEntity();
        player.AddOrUpdate(new IdentifierComponent("Player", "player"));
        player.AddOrUpdate(new TransformComponent(Vector3.Zero, Quaternion.Identity));
        player.Add<PlayerComponent>();
    }
}