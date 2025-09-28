using System.Drawing;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.UI;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.Bricks;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.IO;
using Swordfish.Physics;
using Swordfish.Types;
using Swordfish.UI.Reef;
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
    private readonly IWindowContext _windowContext;
    private readonly BrickEntityBuilder _brickEntityBuilder;
    private readonly BrickDatabase _brickDatabase;
    private readonly ReefContext _reefContext;

    public Entry(
        in ILogger<Entry> logger,
        in IECSContext ecsContext,
        in IPhysics physics,
        in IShortcutService shortcutService,
        in IWindowContext windowContext,
        in BrickEntityBuilder brickEntityBuilder,
        in IFileParseService fileParseService,
        in BrickDatabase brickDatabase,
        in ReefContext reefContext
    ) {
        _ecsContext = ecsContext;
        _physics = physics;
        _windowContext = windowContext;
        _brickEntityBuilder = brickEntityBuilder;
        _brickDatabase = brickDatabase;
        _reefContext = reefContext;
        
        windowContext.SetTitle("Wayward Beyond");        
        logger.LogInformation("Starting Wayward Beyond {version}", WaywardBeyond.Version);
        
        Shortcut quitShortcut = new(
            "Quit Game",
            "General",
            ShortcutModifiers.None,
            Key.Esc,
            Shortcut.DefaultEnabled,
            _windowContext.Close
        );
        shortcutService.RegisterShortcut(quitShortcut);
        
        windowContext.Update += OnWindowUpdate;
    }

    private void OnWindowUpdate(double delta)
    {
        UIBuilder<Material> ui = _reefContext.Builder;

        using (ui.Text($"WORK IN PROGRESS | {WaywardBeyond.Version}"))
        {
            ui.FontSize = 20;
            ui.Color = new Vector4(0.5f);
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Top,
                X = new Relative(0.5f),
                Y = new Relative(0.03f),
            };
        }
    }

    public void Run()
    {
        _physics.SetGravity(Vector3.Zero);

        var worldGenerator = new WorldGenerator("wayward beyond", _brickEntityBuilder, _brickDatabase);
        Task.Run(worldGenerator.Generate);

        var shipGrid = new BrickGrid(dimensionSize: 16);
        shipGrid.Set(0, 0, 0, _brickDatabase.Get("ship_core").Value.ToBrick());
        _brickEntityBuilder.Create("ship", shipGrid, Vector3.Zero, Quaternion.Identity, Vector3.One);
        
        Entity player = _ecsContext.World.NewEntity();
        var inventory = new InventoryComponent(size: 9);
        player.Add<PlayerComponent>();
        player.Add<EquipmentComponent>();
        player.AddOrUpdate(new IdentifierComponent("Player", "player"));
        player.AddOrUpdate(new TransformComponent(Vector3.Zero, Quaternion.Identity));
        player.AddOrUpdate(inventory);
        inventory.Contents[0] = new ItemStack("panel", count: 50);
        inventory.Contents[1] = new ItemStack("thruster", count: 10);
        inventory.Contents[2] = new ItemStack("display_control", count: 10);
        inventory.Contents[3] = new ItemStack("caution_panel", count: 10);
        inventory.Contents[4] = new ItemStack("glass", count: 10);
        inventory.Contents[5] = new ItemStack("display_monitor", count: 5);
        inventory.Contents[6] = new ItemStack("storage", count: 10);
        inventory.Contents[7] = new ItemStack("truss", count: 50);
        inventory.Contents[8] = new ItemStack("laser", count: 1);
    }
}