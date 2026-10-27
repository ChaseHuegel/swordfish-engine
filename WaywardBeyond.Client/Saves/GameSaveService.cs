using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WaywardBeyond.Client.Globalization;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Levels;

namespace WaywardBeyond.Client.Saves;

/// <summary>
/// The client's thin view over level persistence. The server owns the authoritative level, its
/// generation, and its save database; this service only maintains a cached save listing for the menu
/// (queried from the server) and issues create/delete/save requests. "Last played" and "time played" shown
/// in the listing are the client's own per-save stats, tracked in <see cref="ILevelMetadataStorage"/> and merged
/// over the server's level metadata here. Characters remain client-owned and are handled separately by
/// <see cref="CharacterSaveManager"/>.
/// </summary>
internal sealed class GameSaveService(
    in ILogger<GameSaveService> logger,
    in LocalizedFormatter localizedFormatter,
    in NotificationService notificationService,
    in LevelsClient levelsClient,
    in ILevelMetadataStorage levelMetadataStorage
) {
    private readonly ILogger _logger = logger;
    private readonly LocalizedFormatter _localizedFormatter = localizedFormatter;
    private readonly NotificationService _notificationService = notificationService;
    private readonly LevelsClient _levelsClient = levelsClient;
    private readonly ILevelMetadataStorage _levelMetadataStorage = levelMetadataStorage;

    private readonly object _savesGate = new();
    private Level[] _levels = [];
    private Dictionary<string, LevelMetadata> _clientMeta = new();
    private bool _metaLoaded;
    private bool _canCreateSave;
    private int _listingGeneration;

    /// <summary>True when the server reports that this client may create saves.</summary>
    public bool CanCreateSave
    {
        get
        {
            lock (_savesGate)
            {
                return _canCreateSave;
            }
        }
    }

    /// <summary>Clears the cached listing while the active server changes.</summary>
    public void ClearListing()
    {
        lock (_savesGate)
        {
            _levels = [];
            _canCreateSave = false;
            _listingGeneration++;
        }
    }

    public GameSave[] GetSaves()
    {
        lock (_savesGate)
        {
            var saves = new GameSave[_levels.Length];
            for (var i = 0; i < _levels.Length; i++)
            {
                Level level = _levels[i];
                if (_clientMeta.TryGetValue(level.Guid, out LevelMetadata meta))
                {
                    level = level with
                    {
                        Playtime = meta.Playtime
                    };
                }
                else
                {
                    level = level with
                    {
                        Playtime = default
                    };
                }

                saves[i] = new GameSave(level.Name, level);
            }

            SortByLastPlayed(saves);
            return saves;
        }
    }

    /// <summary>Sorts saves newest first and preserves the listing order for equal timestamps.</summary>
    internal static void SortByLastPlayed(GameSave[] saves)
    {
        for (var i = 1; i < saves.Length; i++)
        {
            GameSave current = saves[i];
            long lastPlayed = current.Level.Playtime.LastPlayedMs;
            var j = i - 1;
            while (j >= 0 && saves[j].Level.Playtime.LastPlayedMs < lastPlayed)
            {
                saves[j + 1] = saves[j];
                j--;
            }

            saves[j + 1] = current;
        }
    }

    public LevelMetadata? GetMetadata(string levelGuid)
    {
        lock (_savesGate)
        {
            return _clientMeta.TryGetValue(levelGuid, out LevelMetadata meta) ? meta : null;
        }
    }

    /// <summary>Records a client save's metadata and persists it to the client's own bucket.</summary>
    public void UpdateMetadata(string levelGuid, LevelMetadata meta)
    {
        lock (_savesGate)
        {
            _clientMeta[levelGuid] = meta;
        }

        _levelMetadataStorage.Save(levelGuid, meta);
    }

    /// <summary>Ends a save's session on this client by stamping the current wall-clock as last played.</summary>
    public void BeginSaveSession(string levelGuid)
    {
        if (string.IsNullOrEmpty(levelGuid))
        {
            return;
        }

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        lock (_savesGate)
        {
            _clientMeta.TryGetValue(levelGuid, out LevelMetadata meta);
            _clientMeta[levelGuid] = new LevelMetadata
            {
                Playtime = meta.Playtime with
                {
                    LastPlayedMs = nowMs
                },
            };
        }
    }

    /// <summary>
    /// Refreshes the cached save listing from the server-owned <c>levels</c> bucket (via a
    /// <see cref="ListLevelsRequest"/>). The client no longer owns level metadata; it holds only this
    /// cached view for the menu, merged with its own per-save stats.
    /// </summary>
    public async Task RefreshLevelsAsync()
    {
        int generation;
        lock (_savesGate)
        {
            generation = _listingGeneration;
        }

        try
        {
            LevelListing listing = await _levelsClient.GetLevelsAsync();

            lock (_savesGate)
            {
                if (generation != _listingGeneration)
                {
                    return;
                }

                _levels = listing.Levels;
                _canCreateSave = listing.CanCreateSave;
                if (!_metaLoaded)
                {
                    _clientMeta = new(_levelMetadataStorage.GetAll());
                    _metaLoaded = true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh the level listing from the server.");
        }
    }

    public async Task<GameSave?> CreateSaveAsync(GameOptions options)
    {
        _notificationService.Push(_localizedFormatter.GetString("notification.save.creating", options.Name));
        bool success = await CreateLevelAsync(options.Name, options.Seed);
        if (!success)
        {
            return null;
        }

        GameSave[] saves = GetSaves();
        for (var i = 0; i < saves.Length; i++)
        {
            if (saves[i].Name == options.Name)
            {
                return saves[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Asks the server to flush its authoritative level to the <c>levels</c> bucket. The client no longer
    /// stores level state; quicksave/autosave/pause/close all delegate persistence to the server.
    /// </summary>
    public Task TriggerServerSave()
    {
        return _levelsClient.SaveLevelAsync();
    }

    /// <summary>Notifies the server the player is returning to the menu (end session, free mirror).</summary>
    public void LeaveGame()
    {
        _levelsClient.SendLeaveGame();
    }

    private async Task<bool> CreateLevelAsync(string name, string seed)
    {
        bool success = await _levelsClient.CreateLevelAsync(name, seed, GameMode.Creative);
        await RefreshLevelsAsync();

        _notificationService.Push(_localizedFormatter.GetString(
            success ? "notification.save.created" : "notification.save.creating.failed",
            name
        ));

        return success;
    }

    public void Delete(GameSave save)
    {
        _ = DeleteLevelAsync(save.Level.Guid, save.Name);
    }

    private async Task DeleteLevelAsync(string levelGuid, string name)
    {
        bool success = await _levelsClient.DeleteLevelAsync(levelGuid);

        lock (_savesGate)
        {
            _clientMeta.Remove(levelGuid);
        }
        _levelMetadataStorage.Delete(levelGuid);

        await RefreshLevelsAsync();

        _notificationService.Push(_localizedFormatter.GetString(
            success ? "notification.save.deleted" : "notification.save.deleting.failed",
            name
        ));
    }
}
