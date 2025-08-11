using System.Numerics;
using System.Threading.Tasks;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.Bricks;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Generation;
using WaywardBeyond.Client.Core.Items;

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

        var shipGrid = new BrickGrid(dimensionSize: 16);
        shipGrid.Set(0, 0, 0, BrickRegistry.ShipCore);
        _brickEntityBuilder.Create("ship", shipGrid, Vector3.Zero, Quaternion.Identity, Vector3.One);
        
        Entity player = _ecsContext.World.NewEntity();
        var inventory = new InventoryComponent(size: 9);
        player.Add<PlayerComponent>();
        player.AddOrUpdate(new IdentifierComponent("Player", "player"));
        player.AddOrUpdate(new TransformComponent(Vector3.Zero, Quaternion.Identity));
        player.AddOrUpdate(inventory);
        inventory.Contents[0] = new ItemStack(BrickRegistry.MetalPanel.Name!, count: 30);
        inventory.Contents[1] = new ItemStack(BrickRegistry.Thruster.Name!, count: 10);
        inventory.Contents[2] = new ItemStack(BrickRegistry.DisplayControl.Name!, count: 10);
        inventory.Contents[3] = new ItemStack(BrickRegistry.CautionPanel.Name!, count: 10);
    }
}