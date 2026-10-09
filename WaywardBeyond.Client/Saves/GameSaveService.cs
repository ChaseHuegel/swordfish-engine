using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WaywardBeyond.Client.Globalization;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.Saves;

/// <summary>
/// The client's thin view over level persistence. The server owns the authoritative level, its
/// generation, and its save database; this service only maintains a cached save listing for the menu
/// (queried from the server) and issues create/delete/save requests. "Last played" and "time played" shown
/// in the listing are the client's own per-save stats, tracked in <see cref="ISaveMetaStorage"/> and merged
/// over the server's level metadata here. Characters remain client-owned and are handled separately by
/// <see cref="CharacterSaveManager"/>.
/// </summary>
internal sealed class GameSaveService(
    in ILogger<GameSaveService> logger,
    in LocalizedFormatter localizedFormatter,
    in NotificationService notificationService,
    in LevelsClient levelsClient,
    in ISaveMetaStorage saveMetaStorage
) {
    private readonly ILogger _logger = logger;
    private readonly LocalizedFormatter _localizedFormatter = localizedFormatter;
    private readonly NotificationService _notificationService = notificationService;
    private readonly LevelsClient _levelsClient = levelsClient;
    private readonly ISaveMetaStorage _saveMetaStorage = saveMetaStorage;

    private readonly object _savesGate = new();
    private Level[] _levels = [];
    private Dictionary<string, SaveMeta> _clientMeta = new();
    private bool _metaLoaded;

    public GameSave[] GetSaves()
    {
        lock (_savesGate)
        {
            var saves = new GameSave[_levels.Length];
            for (var i = 0; i < _levels.Length; i++)
            {
                Level level = _levels[i];
                if (_clientMeta.TryGetValue(level.Guid, out SaveMeta meta))
                {
                    level = level with { LastPlayedMs = meta.LastPlayedMs, AgeMs = meta.AgeMs };
                }
                else
                {
                    level = level with { LastPlayedMs = 0, AgeMs = 0 };
                }

                saves[i] = new GameSave(level.Name, level);
            }

            return saves;
        }
    }

    public SaveMeta? GetSaveMeta(string levelGuid)
    {
        lock (_savesGate)
        {
            return _clientMeta.TryGetValue(levelGuid, out SaveMeta meta) ? meta : null;
        }
    }

    /// <summary>Records a client save's metadata and persists it to the client's own bucket.</summary>
    public void UpdateSaveMeta(string levelGuid, SaveMeta meta)
    {
        lock (_savesGate)
        {
            _clientMeta[levelGuid] = meta;
        }

        _saveMetaStorage.Save(levelGuid, meta);
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
            _clientMeta.TryGetValue(levelGuid, out SaveMeta meta);
            _clientMeta[levelGuid] = new SaveMeta
            {
                LastPlayedMs = nowMs,
                AgeMs = meta.AgeMs,
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
        try
        {
            Level[] levels = await _levelsClient.GetLevelsAsync();

            lock (_savesGate)
            {
                _levels = levels;
                if (!_metaLoaded)
                {
                    _clientMeta = _saveMetaStorage.GetAll()
                        .ToDictionary(pair => pair.Key, pair => pair.Value);
                    _metaLoaded = true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh the level listing from the server.");
        }
    }

    public void CreateSave(GameOptions options)
    {
        _notificationService.Push(_localizedFormatter.GetString("notification.save.creating", options.Name));
        _ = CreateLevelAsync(options.Name, options.Seed);
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

    private async Task CreateLevelAsync(string name, string seed)
    {
        bool success = await _levelsClient.CreateLevelAsync(name, seed, GameMode.Creative);
        await RefreshLevelsAsync();

        _notificationService.Push(_localizedFormatter.GetString(
            success ? "notification.save.created" : "notification.save.creating.failed",
            name
        ));
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
        _saveMetaStorage.Delete(levelGuid);

        await RefreshLevelsAsync();

        _notificationService.Push(_localizedFormatter.GetString(
            success ? "notification.save.deleted" : "notification.save.deleting.failed",
            name
        ));
    }
}