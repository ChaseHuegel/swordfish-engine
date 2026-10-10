using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Transport;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Serves the in-world level save path. The server owns the autosave cadence: while a world has
/// sessions it queues an authoritative flush every <c>GameplayConfig.AutosaveIntervalMs</c>. A client
/// <see cref="SaveLevelRequest"/> is accepted only with the <see cref="GamePermissions.LevelSave"/>
/// permission; the local host always qualifies. Every queued save broadcasts a start toast to the
/// world, and each finished database write broadcasts the result.
/// </summary>
public sealed class ServerWorldSystem : IServerWorldSystem
{
    private readonly ServerConnectionHub _hub;
    private readonly SessionManager _sessions;
    private readonly LevelSaveService _saveService;
    private readonly IUserPermissionService _permissions;
    private readonly GameplayConfig _gameplayConfig;
    private readonly ILogger<ServerWorldSystem> _logger;

    private float _autosaveElapsedSeconds;

    public ServerWorldSystem(
        in ServerConnectionHub hub,
        SessionManager sessions,
        in LevelSaveService saveService,
        in IUserPermissionService permissions,
        in GameplayConfig gameplayConfig,
        in ILogger<ServerWorldSystem> logger
    ) {
        _hub = hub;
        _sessions = sessions;
        _saveService = saveService;
        _permissions = permissions;
        _gameplayConfig = gameplayConfig;
        _logger = logger;
    }

    public void Tick(float delta, DataStore store)
    {
        HandleAutosave(delta, store);
        HandleSaveRequests(store);
        BroadcastSaveResults();
    }

    /// <summary>
    /// Queues an authoritative flush on the configured interval. An idle world does not autosave; the
    /// world unload path already flushes a world with no sessions.
    /// </summary>
    private void HandleAutosave(float delta, DataStore store)
    {
        if (_sessions.Count == 0)
        {
            _autosaveElapsedSeconds = 0f;
            return;
        }

        if (!_gameplayConfig.Autosave.Get())
        {
            return;
        }

        _autosaveElapsedSeconds += delta;
        float intervalSeconds = _gameplayConfig.AutosaveIntervalMs.Get() / 1000f;
        if (intervalSeconds <= 0f || _autosaveElapsedSeconds < intervalSeconds)
        {
            return;
        }

        _autosaveElapsedSeconds = 0f;
        QueueSave(store);
    }

    private void HandleSaveRequests(DataStore store)
    {
        foreach ((Uuid clientId, _) in _hub.Receive<SaveLevelRequest>())
        {
            if (!_permissions.HasPermission(clientId, GamePermissions.LevelSave))
            {
                _logger.LogInformation(
                    "Denied a level save for client {clientId}: the client lacks the \"{permission}\" permission.",
                    clientId, GamePermissions.LevelSave);

                Result response = _hub.Send(clientId, new SaveLevelResponse { Success = false });
                if (!response.Success)
                {
                    _logger.LogWarning("Failed to send the save denial to client {clientId}: {message}.", clientId, response.Message);
                }

                SendNotification(clientId, "notification.save.denied");
                continue;
            }

            QueueSave(store);

            Result accepted = _hub.Send(clientId, new SaveLevelResponse { Success = true });
            if (!accepted.Success)
            {
                _logger.LogWarning("Failed to send the save response to client {clientId}: {message}.", clientId, accepted.Message);
            }
        }
    }

    /// <summary>
    /// Captures the authoritative level on the server thread, then broadcasts the start toast. No-op
    /// when no level is loaded.
    /// </summary>
    private void QueueSave(DataStore store)
    {
        if (string.IsNullOrEmpty(_saveService.CurrentLevelGuid))
        {
            return;
        }

        _saveService.QueueSave(store);
        BroadcastNotification("notification.save.saving");
    }

    /// <summary>Broadcasts the outcome of every finished database write.</summary>
    private void BroadcastSaveResults()
    {
        while (_saveService.TryDequeueCompletion(out bool success))
        {
            BroadcastNotification(success ? "notification.save.saved" : "notification.save.saving.failed");
        }
    }

    private void BroadcastNotification(string key)
    {
        string levelName = _saveService.CurrentLevel?.Name ?? string.Empty;

        foreach ((Uuid clientId, _) in _hub.Clients)
        {
            Result send = _hub.Send(clientId, new NotificationMessage
            {
                Type = (byte)NotificationType.Toast,
                Key = key,
                Args = [levelName],
            });

            if (!send.Success)
            {
                _logger.LogWarning("Failed to send the save notification to client {clientId}: {message}.", clientId, send.Message);
            }
        }
    }

    private void SendNotification(Uuid clientId, string key)
    {
        Result send = _hub.Send(clientId, new NotificationMessage
        {
            Type = (byte)NotificationType.Toast,
            Key = key,
            Args = [],
        });

        if (!send.Success)
        {
            _logger.LogWarning("Failed to send the save notification to client {clientId}: {message}.", clientId, send.Message);
        }
    }
}
