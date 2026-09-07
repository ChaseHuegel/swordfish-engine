using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WaywardBeyond.Client.Core.Globalization;
using WaywardBeyond.Client.Core.Networking;
using WaywardBeyond.Client.Core.UI;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Client.Core.Saves;

/// <summary>
/// The client's thin view over world persistence. The server owns the <c>levels</c> bucket, world
/// generation and the authoritative world; this service only maintains a cached save listing for the menu
/// (queried from the server) and issues create/delete/save requests. Characters remain client-owned and
/// are handled separately by <see cref="CharacterSaveManager"/>.
/// </summary>
internal sealed class GameSaveService(
    in ILogger<GameSaveService> logger,
    in LocalizedFormatter localizedFormatter,
    in NotificationService notificationService,
    in WorldsClient worldsClient
) {
    private readonly ILogger _logger = logger;
    private readonly LocalizedFormatter _localizedFormatter = localizedFormatter;
    private readonly NotificationService _notificationService = notificationService;
    private readonly WorldsClient _worldsClient = worldsClient;

    private readonly object _savesGate = new();
    private GameSave[] _saves = [];

    public string GetStatus() => "Complete";

    public GameSave[] GetSaves()
    {
        lock (_savesGate)
        {
            return _saves;
        }
    }

    /// <summary>
    /// Refreshes the cached save listing from the server-owned <c>levels</c> bucket (via a
    /// <see cref="ListWorldsRequest"/>). The client no longer owns world metadata; it holds only this
    /// cached view for the menu.
    /// </summary>
    public async Task RefreshWorldsAsync()
    {
        try
        {
            Level[] levels = await _worldsClient.GetLevelsAsync();

            var saves = new GameSave[levels.Length];
            for (var i = 0; i < levels.Length; i++)
            {
                Level level = levels[i];
                saves[i] = new GameSave(level.Name, level);
            }

            lock (_savesGate)
            {
                _saves = saves;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh the world listing from the server.");
        }
    }

    public void CreateSave(GameOptions options)
    {
        _notificationService.Push(_localizedFormatter.GetString("notification.save.creating", options.Name));
        _ = CreateWorldAsync(options.Name, options.Seed);
    }

    /// <summary>
    /// Asks the server to flush its authoritative world to the <c>levels</c> bucket. The client no longer
    /// stores world state; quicksave/autosave/pause/close all delegate persistence to the server.
    /// </summary>
    public Task TriggerServerSave()
    {
        return _worldsClient.SaveWorldAsync();
    }

    private async Task CreateWorldAsync(string name, string seed)
    {
        bool success = await _worldsClient.CreateWorldAsync(name, seed, GameMode.Creative);
        await RefreshWorldsAsync();

        _notificationService.Push(_localizedFormatter.GetString(
            success ? "notification.save.created" : "notification.save.creating.failed",
            name
        ));
    }

    public void Delete(GameSave save)
    {
        _ = DeleteWorldAsync(save.Level.Guid, save.Name);
    }

    private async Task DeleteWorldAsync(string levelGuid, string name)
    {
        bool success = await _worldsClient.DeleteWorldAsync(levelGuid);
        await RefreshWorldsAsync();

        _notificationService.Push(_localizedFormatter.GetString(
            success ? "notification.save.deleted" : "notification.save.deleting.failed",
            name
        ));
    }
}