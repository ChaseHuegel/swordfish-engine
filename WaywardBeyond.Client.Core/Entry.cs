using System.Drawing;
using System.Numerics;
using System.Threading.Tasks;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.Bricks;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.IO;
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

    public Entry(
        in IECSContext ecsContext,
        in IPhysics physics,
        in IShortcutService shortcutService,
        in IWindowContext windowContext,
        in BrickEntityBuilder brickEntityBuilder,
        in IFileParseService fileParseService)
    {
        _ecsContext = ecsContext;
        _physics = physics;
        _shortcutService = shortcutService;
        _windowContext = windowContext;
        _brickEntityBuilder = brickEntityBuilder;
        _fileParseService = fileParseService;
        
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

        var uiShader = _fileParseService.Parse<Shader>(AssetPaths.Shaders.At("ui_default.glsl"));
        var uiTexture = _fileParseService.Parse<Texture>(AssetPaths.Textures.At("ui_default.png"));
        var uiMaterial = new Material(uiShader, uiTexture);
        
        // Entity uiEntity = _ecsContext.World.NewEntity();
        // var rect = new Rect2(new Vector2(0.31f, 0.02f), new Vector2(0.69f, 0.1f));
        // var rectRenderer = new RectRenderer(rect, Color.Black, uiMaterial);
        // uiEntity.AddOrUpdate(new RectRendererComponent(rectRenderer));
        
        var crosshairShader = _fileParseService.Parse<Shader>(AssetPaths.Shaders.At("crosshair.glsl"));
        var crosshairTexture = _fileParseService.Parse<Texture>(AssetPaths.Textures.At("crosshair.png"));
        var crosshairMaterial = new Material(crosshairShader, crosshairTexture);
        Entity crosshairEntity = _ecsContext.World.NewEntity();
        var offset = new Vector2(0.01f, 0.01777777778f) * 1.5f;
        var crosshairRect = new Rect2(new Vector2(0.5f) - offset, new Vector2(0.5f) + offset);
        var crosshairRenderer = new RectRenderer(crosshairRect, Color.White, crosshairMaterial);
        crosshairEntity.AddOrUpdate(new RectRendererComponent(crosshairRenderer));
    }
}