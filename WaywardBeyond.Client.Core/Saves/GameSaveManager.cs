using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Shoal.DependencyInjection;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Types.Shapes;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Items;
using WaywardBeyond.Client.Core.UI.Layers.Menus.Main;

namespace WaywardBeyond.Client.Core.Saves;

internal sealed class GameSaveManager : IAutoActivate, IDisposable
{
    private readonly struct GameLoadContext : IDisposable
    {
        private readonly GameSave _save;
        private readonly IECSContext _ecs;
        
        public GameLoadContext(GameSave save, IPhysics physics, IECSContext ecs)
        {
            _save = save;
            _ecs = ecs;
            WaywardBeyond.GameState.Set(GameState.Loading);
            physics.SetGravity(Vector3.Zero);
        }
        
        public void Dispose()
        {
            Entity player = _ecs.World.NewEntity();
            var inventory = new InventoryComponent(size: 9);
            player.Add<PlayerComponent>();
            player.Add<EquipmentComponent>();
            player.AddOrUpdate(new IdentifierComponent("Player", "player"));
            player.AddOrUpdate(new TransformComponent(new Vector3(_save.Level.SpawnX, _save.Level.SpawnY, _save.Level.SpawnZ), Quaternion.Identity));
            player.AddOrUpdate(new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous));
            
            var playerCapsule = new Shape(new Box3(new Vector3(0.25f, 1.7f, 0.25f)));
            var playerCollider = new CompoundShape([playerCapsule], [new Vector3(0f, -0.75f, 0f)], [Quaternion.Identity]);
            player.AddOrUpdate(new ColliderComponent(playerCollider));
            
            player.AddOrUpdate(inventory);
            inventory.Contents[0] = new ItemStack("panel", count: 1000);
            inventory.Contents[1] = new ItemStack("thruster", count: 1000);
            inventory.Contents[2] = new ItemStack("display_control", count: 1000);
            inventory.Contents[3] = new ItemStack("caution_panel", count: 1000);
            inventory.Contents[4] = new ItemStack("glass", count: 1000);
            inventory.Contents[5] = new ItemStack("display_monitor", count: 1000);
            inventory.Contents[6] = new ItemStack("storage", count: 1000);
            inventory.Contents[7] = new ItemStack("truss", count: 1000);
            inventory.Contents[8] = new ItemStack("small_light", count: 1000);
        
            WaywardBeyond.GameState.Set(GameState.Playing);
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
    private readonly IECSContext _ecs;
    private readonly IPhysics _physics;

    private readonly Timer _autosaveTimer;
    
    private readonly Lock _activeSaveLock = new();
    private GameSave? _activeSave;
    
    public GameSaveManager(
        in GameSaveService gameSaveService,
        in IWindowContext windowContext,
        in IShortcutService shortcutService,
        in IECSContext ecs,
        in IPhysics physics
    ) {
        _gameSaveService = gameSaveService;
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
        
        //  Automatically save on an interval, on close, and when pausing.
        _autosaveTimer = new Timer(OnAutosave, state: null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        windowContext.Closed += OnWindowClosed;
        WaywardBeyond.GameState.Changed += OnGameStateChanged;
    }

    public void Dispose()
    {
        _autosaveTimer.Dispose();
    }

    public async Task NewGame(GameOptions options)
    {
        GameSave save;
        lock (_activeSaveLock)
        {
            if (ActiveSave != null)
            {
                return;
            }
            
            save = _gameSaveService.CreateSave(options);
            ActiveSave = save;
        }
        
        using var gameLoadContext = new GameLoadContext(save, _physics, _ecs);
        await _gameSaveService.GenerateSaveData(options);
        await Save();
    }
    
    public Task Load()
    {
        GameSave save;
        lock (_activeSaveLock)
        {
            if (ActiveSave == null)
            {
                return Task.CompletedTask;
            }
            
            save = ActiveSave.Value;
        }
        
        long nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Level level = save.Level with
        {
            LastPlayedMs = nowUtcMs,
        };
        
        save = new GameSave(save.Path, save.Name, level);

        using var gameLoadContext = new GameLoadContext(save, _physics, _ecs);
        return _gameSaveService.Load(save);
    }
    
    public Task Save()
    {
        if (WaywardBeyond.GameState < GameState.Playing)
        {
            return Task.CompletedTask;
        }
        
        using Lock.Scope _ = _activeSaveLock.EnterScope();
        
        if (ActiveSave == null)
        {
            return Task.CompletedTask;
        }

        GameSave save = ActiveSave.Value;
        
        long nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Level level = save.Level with
        {
            AgeMs = nowUtcMs - save.Level.LastPlayedMs,
        };
        
        save = new GameSave(save.Path, save.Name, level);
        
        _gameSaveService.Save(save);
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
    
    private void OnAutosave(object? state)
    {
        Save();
    }
    
    private void OnGameStateChanged(object? sender, DataChangedEventArgs<GameState> e)
    {
        if (e.NewValue != GameState.Paused)
        {
            return;
        }
        
        Save();
    }
    
    public void SaveAndExit()
    {
        if (WaywardBeyond.GameState < GameState.Playing)
        {
            return;
        }

        Save();
        ActiveSave = null;
        
        //  TODO should create a new ECS world instead of trying to cleanup state
        
        _ecs.World.DataStore.Query<IdentifierComponent>(0f, CleanupGameEntitiesQuery);
        void CleanupGameEntitiesQuery(float delta, DataStore store, int entity, ref IdentifierComponent identifier)
        {
            if (identifier.Tag != "game")
            {
                return;
            }
            
            if (store.TryGet(entity, out MeshRendererComponent meshRendererComponent))
            {
                meshRendererComponent.MeshRenderer.Dispose();
                meshRendererComponent.MeshRenderer.Mesh.Dispose();
            }
            
            if (store.TryGet(entity, out PhysicsComponent physicsComponent))
            {
                physicsComponent.Dispose();
            }
            
            store.Free(entity);
        }
        
        _ecs.World.DataStore.Query<PlayerComponent>(0f, CleanupPlayerQuery);
        void CleanupPlayerQuery(float delta, DataStore store, int entity, ref PlayerComponent player)
        {
            if (store.TryGet(entity, out PhysicsComponent physicsComponent))
            {
                physicsComponent.Dispose();
            }
            
            store.Free(entity);
        }
        
        WaywardBeyond.GameState.Set(GameState.MainMenu);
    }
}