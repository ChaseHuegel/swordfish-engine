using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Shoal.DependencyInjection;
using Swordfish.Bricks;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Generation;
using WaywardBeyond.Client.Core.Items;

namespace WaywardBeyond.Client.Core.Saves;

internal sealed class GameSaveManager : IAutoActivate
{
    private readonly struct GameLoadContext : IDisposable
    {
        private readonly IECSContext _ecs;
        
        public GameLoadContext(IPhysics physics, IECSContext ecs)
        {
            _ecs = ecs;
            WaywardBeyond.GameState = GameState.Loading;
            physics.SetGravity(Vector3.Zero);
        }
        
        public void Dispose()
        {
            Entity player = _ecs.World.NewEntity();
            var inventory = new InventoryComponent(size: 9);
            player.Add<PlayerComponent>();
            player.Add<EquipmentComponent>();
            player.AddOrUpdate(new IdentifierComponent("Player", "player"));
            player.AddOrUpdate(new TransformComponent(new Vector3(0f, 1f, 5f), Quaternion.Identity));
            player.AddOrUpdate(inventory);
            inventory.Contents[0] = new ItemStack("panel", count: 1000);
            inventory.Contents[1] = new ItemStack("thruster", count: 1000);
            inventory.Contents[2] = new ItemStack("display_control", count: 1000);
            inventory.Contents[3] = new ItemStack("caution_panel", count: 1000);
            inventory.Contents[4] = new ItemStack("glass", count: 1000);
            inventory.Contents[5] = new ItemStack("display_monitor", count: 1000);
            inventory.Contents[6] = new ItemStack("storage", count: 1000);
            inventory.Contents[7] = new ItemStack("truss", count: 1000);
            inventory.Contents[8] = new ItemStack("laser", count: 1);
        
            WaywardBeyond.GameState = GameState.Playing;
        }
    }
    
    public GameSave? ActiveSave
    {
        get
        {
            using Lock.Scope _ = _activeSaveLock.EnterScope();
            return _activeSave;
        }
        set
        {
            using Lock.Scope _ = _activeSaveLock.EnterScope();
            _activeSave = value;
        }
    }
    
    private readonly GameSaveService _gameSaveService;
    private readonly BrickEntityBuilder _brickEntityBuilder;
    private readonly BrickDatabase _brickDatabase;
    private readonly IECSContext _ecs;
    private readonly IPhysics _physics;
    
    private readonly Lock _activeSaveLock = new();
    private GameSave? _activeSave;
    
    public GameSaveManager(
        in GameSaveService gameSaveService,
        in IWindowContext windowContext,
        in IShortcutService shortcutService,
        in BrickEntityBuilder brickEntityBuilder,
        in BrickDatabase brickDatabase,
        in IECSContext ecs,
        in IPhysics physics
    ) {
        _gameSaveService = gameSaveService;
        _brickEntityBuilder = brickEntityBuilder;
        _brickDatabase = brickDatabase;
        _ecs = ecs;
        _physics = physics;
        
        Shortcut saveShortcut = new(
            "Quicksave",
            "General",
            ShortcutModifiers.None,
            Key.F5,
            Shortcut.DefaultEnabled,
            OnQuicksave
        );
        shortcutService.RegisterShortcut(saveShortcut);
        
        windowContext.Closed += OnWindowClosed;
    }

    public async Task NewGame(GameOptions options)
    {
        lock (_activeSaveLock)
        {
            if (ActiveSave != null)
            {
                return;
            }
            
            ActiveSave = _gameSaveService.CreateSave("playtest");
        }
        
        using var gameLoadContext = new GameLoadContext(_physics, _ecs);
        
        var shipGrid = new BrickGrid(dimensionSize: 16);
        shipGrid.Set(0, 0, 0, _brickDatabase.Get("ship_core").Value.ToBrick());
        _brickEntityBuilder.Create("ship", shipGrid, Vector3.Zero, Quaternion.Identity, Vector3.One);
        
        var worldGenerator = new WorldGenerator(options.Seed, _brickEntityBuilder, _brickDatabase);
        await worldGenerator.Generate();

        await Save();
    }
    
    public Task Load()
    {
        GameSave activeSave;
        lock (_activeSaveLock)
        {
            if (ActiveSave == null)
            {
                return Task.CompletedTask;
            }

            activeSave = ActiveSave.Value;
        }

        using var gameLoadContext = new GameLoadContext(_physics, _ecs);
        _gameSaveService.Load(activeSave);
        return Task.CompletedTask;
    }
    
    public Task Save()
    {
        using Lock.Scope _ = _activeSaveLock.EnterScope();
        
        if (ActiveSave == null)
        {
            return Task.CompletedTask;
        }
        
        _gameSaveService.Save(ActiveSave.Value);
        return Task.CompletedTask;
    }
    
    private void OnWindowClosed()
    {
        Save();
    }

    private void OnQuicksave()
    {
        Task.Run(Save);
    }
}