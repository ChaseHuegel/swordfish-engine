using System;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using HardwareInformation;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.Bricks;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization;
using Swordfish.Library.Util;
using Swordfish.Physics;
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
    private readonly BrickEntityBuilder _brickEntityBuilder;
    private readonly BrickDatabase _brickDatabase;
    private readonly ReefContext _reefContext;
    private readonly ISerializer<BrickGrid> _brickGridSerializer;
    
    private Entity _ship;

    public Entry(
        in ILogger<Entry> logger,
        in IECSContext ecsContext,
        in IPhysics physics,
        in IShortcutService shortcutService,
        in IWindowContext windowContext,
        in BrickEntityBuilder brickEntityBuilder,
        in BrickDatabase brickDatabase,
        in ReefContext reefContext,
        in ISerializer<BrickGrid> brickGridSerializer
    ) {
        _ecsContext = ecsContext;
        _physics = physics;
        IWindowContext windowContext1 = windowContext;
        _brickEntityBuilder = brickEntityBuilder;
        _brickDatabase = brickDatabase;
        _reefContext = reefContext;
        _brickGridSerializer = brickGridSerializer;
        
        Shortcut quitShortcut = new(
            "Quit Game",
            "General",
            ShortcutModifiers.None,
            Key.Esc,
            Shortcut.DefaultEnabled,
            windowContext1.Close
        );
        shortcutService.RegisterShortcut(quitShortcut);
        
        Shortcut saveShortcut = new(
            "Quicksave",
            "General",
            ShortcutModifiers.None,
            Key.F5,
            Shortcut.DefaultEnabled,
            SaveShip
        );
        shortcutService.RegisterShortcut(saveShortcut);

        windowContext.SetTitle("Wayward Beyond");
        logger.LogInformation("Starting Wayward Beyond {version}", WaywardBeyond.Version);

        MachineInformation? machineInformation = MachineInformationGatherer.GatherInformation();
        if (machineInformation != null)
        {
            logger.LogInformation("OS: {os}", machineInformation.OperatingSystem.VersionString);
            logger.LogInformation("CPU: {cpu}", machineInformation.Cpu.Name); 
            logger.LogInformation("GPU: {gpu} (VRAM: {vram}, Driver: {driverVersion})",
                machineInformation.Gpus[0].Name,
                machineInformation.Gpus[0].AvailableVideoMemoryHRF,
                machineInformation.Gpus[0].DriverVersion
            );
        }
        
        windowContext.Update += OnWindowUpdate;
    }

    private double _currentTime;
    private void OnWindowUpdate(double delta)
    {
        _currentTime += delta;
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

        if (WaywardBeyond.GameState != GameState.Loading)
        {
            return;
        }
        
        using (ui.Element())
        {
            ui.Color = new Vector4(0.25f, 0.25f, 0.25f, 0.75f);
            ui.Constraints = new Constraints
            {
                Width = new Relative(1f),
                Height = new Relative(1f),
            };

            var statusBuilder = new StringBuilder("Generating asteroids");
            int steps = MathS.WrapInt((int)(_currentTime * 2d), 0, 3);
            for (var i = 0; i < steps; i++)
            {
                statusBuilder.Append('.');
            }
                
            using (ui.Text(statusBuilder.ToString()))
            {
                ui.FontSize = 30;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                    X = new Relative(0.5f),
                    Y = new Relative(0.5f),
                };
            }
        }
    }

    public void Run()
    {
        Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        _physics.SetGravity(Vector3.Zero);
        
        var worldGenerator = new WorldGenerator("wayward beyond", _brickEntityBuilder, _brickDatabase);
        await worldGenerator.Generate();

        BrickGrid shipGrid = LoadOrCreateShip();
        _ship = _brickEntityBuilder.Create("ship", shipGrid, Vector3.Zero, Quaternion.Identity, Vector3.One);
        
        Entity player = _ecsContext.World.NewEntity();
        var inventory = new InventoryComponent(size: 9);
        player.Add<PlayerComponent>();
        player.Add<EquipmentComponent>();
        player.AddOrUpdate(new IdentifierComponent("Player", "player"));
        player.AddOrUpdate(new TransformComponent(new Vector3(0f, 1f, 5f), Quaternion.Identity));
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
        WaywardBeyond.GameState = GameState.Playing;
    }

    private void SaveShip()
    {
        BrickComponent? brickComponent = _ship.Get<BrickComponent>();
        if (brickComponent == null)
        {
            return;
        }
        
        byte[] data = _brickGridSerializer.Serialize(brickComponent.Value.Grid);
        using var dataStream = new MemoryStream(data);
        var saveDirectory = new PathInfo("saves");
        Directory.CreateDirectory(saveDirectory);
        PathInfo savePath = saveDirectory.At("ship.dat");
        savePath.Write(dataStream);
    }

    private BrickGrid LoadOrCreateShip()
    {
        var saveDirectory = new PathInfo("saves");
        PathInfo savePath = saveDirectory.At("ship.dat");
        
        if (!savePath.FileExists())
        {
            var brickGrid = new BrickGrid(dimensionSize: 16);
            brickGrid.Set(0, 0, 0, _brickDatabase.Get("ship_core").Value.ToBrick());
            return brickGrid;
        }

        byte[] data = savePath.ReadBytes();
        return _brickGridSerializer.Deserialize(data);
    }
}