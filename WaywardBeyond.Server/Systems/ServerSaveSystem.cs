using Microsoft.Extensions.Logging;
using NsdRpc;
using Swordfish.ECS;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Serves the in-world level save path. The server owns the autosave cadence: while a world has
/// sessions it queues an authoritative flush every <c>GameplayConfig.AutosaveIntervalMs</c>. A client
/// save call is accepted only with the <see cref="GamePermissions.LEVEL_SAVE"/> permission; the local
/// host always qualifies. Every queued save broadcasts a start toast to the world, and each finished
/// database write broadcasts the result.
/// </summary>
public sealed class ServerSaveSystem(
    ServerPeers peers,
    SessionManager sessions,
    LevelSaveService saveService,
    IUserPermissionService permissions,
    GameplayConfig gameplayConfig,
    ILogger<ServerSaveSystem> logger
) : IServerWorldSystem
{
    private readonly ILogger _logger = logger;

    private float _autosaveElapsedSeconds;

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        HandleAutosave(delta, store);
        HandleSaveRequests(store);
        BroadcastSaveResults(store);
    }

    /// <summary>
    /// Queues an authoritative flush on the configured interval. An idle world does not autosave; the
    /// world unload path already flushes a world with no sessions.
    /// </summary>
    private void HandleAutosave(float delta, DataStore store)
    {
        if (sessions.Count == 0)
        {
            _autosaveElapsedSeconds = 0f;
            return;
        }

        if (!gameplayConfig.Autosave.Get())
        {
            return;
        }

        _autosaveElapsedSeconds += delta;
        float intervalSeconds = gameplayConfig.AutosaveIntervalMs.Get() / 1000f;
        if (intervalSeconds <= 0f || _autosaveElapsedSeconds < intervalSeconds)
        {
            return;
        }

        _autosaveElapsedSeconds = 0f;
        QueueSave(store);
    }

    private void HandleSaveRequests(DataStore store)
    {
        foreach (Uuid clientId in sessions.Clients)
        {
            if (!peers.TryGet(clientId, out ServerPeer peer))
            {
                continue;
            }

            while (peer.SaveCalls.TryDequeue(out ServerPeer.SaveLevelCall call))
            {
                HandleSaveRequest(store, peer, call);
            }
        }
    }

    private void HandleSaveRequest(DataStore store, ServerPeer peer, in ServerPeer.SaveLevelCall call)
    {
        if (call.CancellationToken.IsCancellationRequested)
        {
            call.Completion.TrySetResult(RpcResult<SaveLevelResponse>.Failure(
                GameRpcErrors.INVALID_STATE, "the save request was cancelled"));
            return;
        }

        if (!permissions.HasPermission(peer.GameId, GamePermissions.LEVEL_SAVE))
        {
            _logger.LogInformation(
                "Denied a level save for client {clientId}: the client lacks the \"{permission}\" permission.",
                peer.GameId, GamePermissions.LEVEL_SAVE);

            call.Completion.TrySetResult(RpcResult<SaveLevelResponse>.Failure(
                GameRpcErrors.PERMISSION_DENIED, "level saving is not permitted"));
            var denied = new NotificationMessage
            {
                Type = NotificationType.Toast,
                Key = "notification.save.denied",
                Args = [],
            };

            if (!peer.Rpc.Notify(GameRpc.Methods.NOTIFICATION, in denied))
            {
                peer.CloseOnRejectedSend(_logger);
            }

            return;
        }

        QueueSave(store);
        call.Completion.TrySetResult(RpcResult<SaveLevelResponse>.Success(new SaveLevelResponse { Success = true }));
    }

    /// <summary>
    /// Captures the authoritative level on the server thread, then broadcasts the start toast. No-op
    /// when no level is loaded.
    /// </summary>
    private void QueueSave(DataStore store)
    {
        if (string.IsNullOrEmpty(saveService.CurrentLevelGuid))
        {
            return;
        }

        saveService.QueueSave(store);
        BroadcastNotification("notification.save.saving");
    }

    /// <summary>Broadcasts the outcome of every finished database write.</summary>
    private void BroadcastSaveResults(DataStore store)
    {
        while (saveService.TryDequeueCompletion(out bool success))
        {
            BroadcastNotification(success ? "notification.save.saved" : "notification.save.saving.failed");
        }
    }

    private void BroadcastNotification(string key)
    {
        string levelName = saveService.CurrentLevel?.Name ?? string.Empty;
        var message = new NotificationMessage
        {
            Type = NotificationType.Toast,
            Key = key,
            Args = [levelName],
        };

        foreach (Uuid clientId in sessions.Clients)
        {
            if (peers.TryGet(clientId, out ServerPeer peer) && !peer.Rpc.Notify(GameRpc.Methods.NOTIFICATION, in message))
            {
                peer.CloseOnRejectedSend(_logger);
            }
        }
    }
}
