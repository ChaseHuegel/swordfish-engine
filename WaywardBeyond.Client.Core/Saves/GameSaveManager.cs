using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Configuration;
using WaywardBeyond.Client.Core.Networking;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Client.Core.Systems;

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

    private readonly ILogger<GameSaveManager> _logger;
    private readonly GameSaveService _gameSaveService;
    private readonly IWindowContext _windowContext;
    private readonly CharacterSaveManager _characterSaveManager;
    private readonly GameplaySettings _gameplaySettings;
    private readonly ClientJoinSystem _joinSystem;
    private readonly ClientCleanupSystem _cleanupSystem;
    private readonly DataStore _dataStore;

    private readonly Lock _autosaveTimerLock = new();
    private Timer _autosaveTimer;
    
    private readonly Lock _activeSaveLock = new();
    private GameSave? _activeSave;

    private readonly TransportManager _transportManager;

    public GameSaveManager(
        in ILogger<GameSaveManager> logger,
        in GameSaveService gameSaveService,
        in IWindowContext windowContext,
        in IShortcutService shortcutService,
        in CharacterSaveManager characterSaveManager,
        in GameplaySettings gameplaySettings,
        in ClientJoinSystem joinSystem,
        in ClientCleanupSystem cleanupSystem,
        in TransportManager transportManager,
        in DataStore dataStore
    ) {
        _logger = logger;
        _gameSaveService = gameSaveService;
        _windowContext = windowContext;
        _characterSaveManager = characterSaveManager;
        _gameplaySettings = gameplaySettings;
        _joinSystem = joinSystem;
        _cleanupSystem = cleanupSystem;
        _transportManager = transportManager;
        _dataStore = dataStore;

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

        //  The save listing is served from the server; start loading it so the menu populates promptly.
        _ = _gameSaveService.RefreshWorldsAsync();
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
        lock (_activeSaveLock)
        {
            if (ActiveSave == null)
            {
                return Task.CompletedTask;
            }

            GameSave save = ActiveSave.Value;

            //  Start the client's own play session clock for this save (meta is persisted on save/leave).
            _gameSaveService.BeginSaveSession(save.Level.Guid);

            //  Stamp the character's clock at session start too; without it the next save would fold the
            //  idle gap since the previous session into the character's time played.
            Result<Character> character = _characterSaveManager.Load();
            if (!character)
            {
                return Task.CompletedTask;
            }

            WaywardBeyond.GameState.Set(GameState.Loading);
            _joinSystem.RequestJoin(character.Value, save.Level.Guid);
        }

        return Task.CompletedTask;
    }
    
    public Task Save()
    {
        if (WaywardBeyond.GameState < GameState.Playing)
        {
            return Task.CompletedTask;
        }
        
        using Lock.Scope activeSaveScope = _activeSaveLock.EnterScope();
        
        if (ActiveSave == null)
        {
            return Task.CompletedTask;
        }

        GameSave save = ActiveSave.Value;

        long nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        SaveMeta meta = _gameSaveService.GetSaveMeta(save.Level.Guid) ?? new SaveMeta();
        meta = new SaveMeta
        {
            AgeMs = SaveTime.Accumulate(meta.AgeMs, meta.LastPlayedMs, nowUtcMs),
            LastPlayedMs = nowUtcMs,
        };
        _gameSaveService.UpdateSaveMeta(save.Level.Guid, meta);
        
        _characterSaveManager.Save(_dataStore);

        //  Only ask the server to flush its authoritative world when a server is actually reachable. After
        //  a remote disconnect the transport is dropped, so the send would target a dead host for nothing.
        if (_transportManager.IsConnected)
        {
            _ = _gameSaveService.TriggerServerSave();
        }

        return Task.CompletedTask;
    }
    
    public void SaveAndExit()
    {
        if (WaywardBeyond.GameState >= GameState.Playing)
        {
            Save();
        }

        //  Drop below Playing BEFORE the cleanup so reconciliation (gated on Playing) stops and cannot
        //  resurrect freed entities from in-flight server snapshots during teardown. The teardown itself
        //  runs on the ECS thread (via ClientCleanupSystem), never the caller's UI thread, so it does not
        //  race the physics/render systems.
        WaywardBeyond.GameState.Set(GameState.MainMenu);
        _cleanupSystem.RequestCleanup();

        //  Ask the server to end this player's session and free its mirror.
        _gameSaveService.LeaveGame();
    }

    public void Delete(GameSave save)
    {
        try
        {
            _gameSaveService.Delete(save);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error trying to delete save \"{save}\"", save.Name);
        }
    }
    
    internal GameSave? GetMostRecentSave()
    {
        GameSave[] saves = _gameSaveService.GetSaves();
        if (saves.Length == 0)
        {
            return null;
        }
        
        return saves.OrderByDescending(save => save.Level.LastPlayedMs).FirstOrDefault();
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
            Character? character = _characterSaveManager.ActiveSave;
            if (character == null)
            {
                _gameSaveManager.SaveAndExit();
                return;
            }

            WaywardBeyond.GameState.Set(GameState.Playing);
        }
    }
}