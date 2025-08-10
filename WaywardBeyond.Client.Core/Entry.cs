using System.Numerics;
using System.Threading.Tasks;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.Bricks;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Physics;
using Swordfish.UI;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Generation;
using WaywardBeyond.Client.Core.UI;

namespace WaywardBeyond.Client.Core;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class Entry : IEntryPoint, IAutoActivate
{
    private readonly IECSContext _ecsContext;
    private readonly IPhysics _physics;
    private readonly IShortcutService _shortcutService;
    private readonly IWindowContext _windowContext;
    private readonly IUIContext _uiContext;
    private readonly BrickEntityBuilder _brickEntityBuilder;

    public Entry(
        in IECSContext ecsContext,
        in IPhysics physics,
        in IShortcutService shortcutService,
        in IWindowContext windowContext,
        in IUIContext uiContext,
        in BrickEntityBuilder brickEntityBuilder)
    {
        _ecsContext = ecsContext;
        _physics = physics;
        _shortcutService = shortcutService;
        _windowContext = windowContext;
        _uiContext = uiContext;
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

        var shipGrid = new BrickGrid(16);
        shipGrid.Set(0, 0, 0, BrickRegistry.ShipCore);
        _brickEntityBuilder.Create("ship", shipGrid, Vector3.Zero, Quaternion.Identity, Vector3.One);
        
        Entity player = _ecsContext.World.NewEntity();
        player.AddOrUpdate(new IdentifierComponent("Player", "player"));
        player.AddOrUpdate(new TransformComponent(Vector3.Zero, Quaternion.Identity));
        player.Add<PlayerComponent>();

        var hotbar = new Hotbar(_windowContext, _uiContext, _shortcutService);
        hotbar.UpdateSlot(0, "Metal Panel", 20);
        hotbar.ActiveSlot.Changed += OnActiveHotbarSlotChanged;
        return;
        
        void OnActiveHotbarSlotChanged(object? sender, DataChangedEventArgs<int> e)
        {
        }
    }
}