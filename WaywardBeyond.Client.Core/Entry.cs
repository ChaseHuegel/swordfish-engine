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
    private readonly IAssetDatabase<Mesh> _meshDatabase;
    private readonly IAssetDatabase<Texture> _textureDatabase;
    private readonly IAssetDatabase<Shader> _shaderDatabase;
    private readonly IAssetDatabase<Material> _materialDatabase;

    public Entry(
        in IECSContext ecsContext,
        in IPhysics physics,
        in IShortcutService shortcutService,
        in IWindowContext windowContext,
        in BrickEntityBuilder brickEntityBuilder,
        in IFileParseService fileParseService,
        in BrickDatabase brickDatabase,
        in IAssetDatabase<Mesh> meshDatabase,
        in IAssetDatabase<Texture> textureDatabase,
        in IAssetDatabase<Shader> shaderDatabase,
        in IAssetDatabase<Material> materialDatabase
    ) {
        _ecsContext = ecsContext;
        _physics = physics;
        _shortcutService = shortcutService;
        _windowContext = windowContext;
        _brickEntityBuilder = brickEntityBuilder;
        _fileParseService = fileParseService;
        _brickDatabase = brickDatabase;
        _meshDatabase = meshDatabase;
        _textureDatabase = textureDatabase;
        _shaderDatabase = shaderDatabase;
        _materialDatabase = materialDatabase;
        
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
        
        Mesh laserMesh = _meshDatabase.Get("items/laser.obj").Value;
        Material laserMaterial = _materialDatabase.Get("laser.toml").Value;
        var laserMeshRenderer = new MeshRenderer(laserMesh, laserMaterial);

        Entity laser = _ecsContext.World.NewEntity();
        laser.AddOrUpdate(new IdentifierComponent("laser"));
        laser.AddOrUpdate(new TransformComponent());
        laser.AddOrUpdate(new MeshRendererComponent(laserMeshRenderer));
        laser.AddOrUpdate(new ChildComponent(player)
        {
            LocalPosition = new Vector3(0.5f, -0.4f, 0.2f),
            LocalOrientation = Quaternion.CreateFromYawPitchRoll(5f * MathS.DEGREES_TO_RADIANS, 0f, 0f),
        });

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