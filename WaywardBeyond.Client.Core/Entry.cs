using System.Drawing;
using System.Numerics;
using System.Threading.Tasks;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.Bricks;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Swordfish.Physics;
using Swordfish.Types;
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
    private readonly IFileParseService _fileParseService;
    private readonly BrickDatabase _brickDatabase;

    public Entry(
        in IECSContext ecsContext,
        in IPhysics physics,
        in IShortcutService shortcutService,
        in IWindowContext windowContext,
        in BrickEntityBuilder brickEntityBuilder,
        in IFileParseService fileParseService,
        in BrickDatabase brickDatabase
    ) {
        _ecsContext = ecsContext;
        _physics = physics;
        _shortcutService = shortcutService;
        _windowContext = windowContext;
        _brickEntityBuilder = brickEntityBuilder;
        _fileParseService = fileParseService;
        _brickDatabase = brickDatabase;
        
        windowContext.SetTitle($"Wayward Beyond {WaywardBeyond.Version}");
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

        var worldGenerator = new WorldGenerator("wayward beyond", _brickEntityBuilder, _brickDatabase);
        Task.Run(worldGenerator.Generate);

        var shipGrid = new BrickGrid(dimensionSize: 16);
        shipGrid.Set(0, 0, 0, _brickDatabase.Get("ship_core").Value.GetBrick());
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
        
        var crosshairShader = _fileParseService.Parse<Shader>(AssetPaths.Shaders.At("crosshair.glsl"));
        var crosshairTexture = _fileParseService.Parse<Texture>(AssetPaths.Textures.At("crosshair.png"));
        var crosshairMaterial = new Material(crosshairShader, crosshairTexture);
        Entity crosshairEntity = _ecsContext.World.NewEntity();
        Vector2 offset = new Vector2(0.01f, 0.01777777778f) * 1.5f;
        var crosshairRect = new Rect2(new Vector2(0.5f) - offset, new Vector2(0.5f) + offset);
        var crosshairRenderer = new RectRenderer(crosshairRect, Color.White, crosshairMaterial);
        crosshairEntity.AddOrUpdate(new RectRendererComponent(crosshairRenderer));
    }
}