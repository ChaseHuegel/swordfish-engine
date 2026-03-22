using System;
using System.Threading;
using System.Threading.Tasks;
using Shoal.DependencyInjection;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Configuration;

namespace WaywardBeyond.Client.Core.Saves;

internal sealed class GameSaveManager : IAutoActivate, IDisposable
{
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
    private readonly IWindowContext _windowContext;
    private readonly IECSContext _ecs;
    private readonly CharacterSaveManager _characterSaveManager;
    private readonly GameplaySettings _gameplaySettings;

    private readonly Lock _autosaveTimerLock = new();
    private Timer _autosaveTimer;
    
    private readonly Lock _activeSaveLock = new();
    private GameSave? _activeSave;
    
    public GameSaveManager(
        in GameSaveService gameSaveService,
        in IWindowContext windowContext,
        in IShortcutService shortcutService,
        in IECSContext ecs,
        in CharacterSaveManager characterSaveManager,
        in GameplaySettings gameplaySettings
    ) {
        _gameSaveService = gameSaveService;
        _windowContext = windowContext;
        _ecs = ecs;
        _characterSaveManager = characterSaveManager;
        _gameplaySettings = gameplaySettings;

        Shortcut saveShortcut = new(
            "Quicksave",
            "General",
            ShortcutModifiers.None,
            Key.F5,
            () => WaywardBeyond.GameState == GameState.Playing,
            OnQuicksave
        );
        shortcutService.RegisterShortcut(saveShortcut);
        
        //  Setup autosave on an interval, on close, and when pausing.
        windowContext.Closed += OnWindowClosed;
        WaywardBeyond.GameState.Changed += OnGameStateChanged;
        
        using Lock.Scope autosaveTimerScope = _autosaveTimerLock.EnterScope();
        int autosaveIntervalMs = gameplaySettings.AutosaveIntervalMs.Get();
        _autosaveTimer = new Timer(OnAutosave, state: null, autosaveIntervalMs, autosaveIntervalMs);
        gameplaySettings.AutosaveIntervalMs.Changed += OnAutosaveIntervalChanged;
    }

    public void Dispose()
    {
        _windowContext.Closed -= OnWindowClosed;
        WaywardBeyond.GameState.Changed -= OnGameStateChanged;
        _gameplaySettings.AutosaveIntervalMs.Changed -= OnAutosaveIntervalChanged;

        using Lock.Scope autosaveTimerScope = _autosaveTimerLock.EnterScope();
        _autosaveTimer.Dispose();
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
            
            long nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Level level = save.Level with
            {
                LastPlayedMs = nowUtcMs,
            };
        
            save = new GameSave(save.Path, save.Name, level);
            ActiveSave = save;
        }

        using var gameLoadContext = new GameLoadContext(_characterSaveManager, gameSaveManager: this);
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
        _characterSaveManager.Save();
        return Task.CompletedTask;
    }
    
    public void SaveAndExit()
    {
        if (WaywardBeyond.GameState >= GameState.Playing)
        {
            Save();
        }

        ActiveSave = null;
        CleanupEcs();
        WaywardBeyond.GameState.Set(GameState.MainMenu);
    }
    
    private void OnWindowClosed()
    {
        if (!_gameplaySettings.Autosave.Get())
        {
            return;
        }
        
        Save();
    }

    private void OnQuicksave()
    {
        Task.Run(Save);
    }
    
    private void OnAutosave(object? state)
    {
        if (!_gameplaySettings.Autosave.Get())
        {
            return;
        }
        
        Save();
    }
    
    private void OnGameStateChanged(object? sender, DataChangedEventArgs<GameState> e)
    {
        if (e.NewValue != GameState.Paused)
        {
            return;
        }
        
        if (!_gameplaySettings.Autosave.Get())
        {
            return;
        }
        
        Save();
    }
    
    private void OnAutosaveIntervalChanged(object? sender, DataChangedEventArgs<int> e)
    {
        UpdateAutosaveTimer(e.NewValue);
    }
    
    private void UpdateAutosaveTimer(int intervalMs)
    {
        using Lock.Scope autosaveTimerScope = _autosaveTimerLock.EnterScope();
        _autosaveTimer.Dispose();
        _autosaveTimer = new Timer(OnAutosave, state: null, intervalMs, intervalMs);
    }

    private void CleanupEcs()
    {
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
    }
    
    private readonly struct GameLoadContext : IDisposable
    {
        private readonly CharacterSaveManager _characterSaveManager;
        private readonly GameSaveManager _gameSaveManager;

        public GameLoadContext(
            CharacterSaveManager characterSaveManager,
            GameSaveManager gameSaveManager
        ) {
            _characterSaveManager = characterSaveManager;
            _gameSaveManager = gameSaveManager;
            WaywardBeyond.GameState.Set(GameState.Loading);
        }
        
        public void Dispose()
        {
            CharacterSave? characterSave = _characterSaveManager.ActiveSave;
            if (characterSave == null)
            {
                _gameSaveManager.SaveAndExit();
                return;
            }

            WaywardBeyond.GameState.Set(GameState.Playing);
        }
    }
}