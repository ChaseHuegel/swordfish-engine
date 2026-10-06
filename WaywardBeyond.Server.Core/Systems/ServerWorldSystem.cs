using System;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Core;
using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core.Systems;

/// <summary>
/// Handles the level save request: the player's pause-menu save queues an authoritative flush of
/// the world through its <see cref="LevelSaveService"/> and replies to the requesting client. Menu-time
/// level management (create/list/delete) is served by <see cref="ServerLevelManager"/> instead, because
/// those requests arrive on connections that have not joined a world yet.
/// </summary>
public sealed class ServerWorldSystem : IServerWorldSystem
{
    private readonly ServerConnectionHub _hub;
    private readonly LevelSaveService _saveService;
    private readonly ILogger<ServerWorldSystem> _logger;

    public ServerWorldSystem(
        in ServerConnectionHub hub,
        in LevelSaveService saveService,
        in ILogger<ServerWorldSystem> logger
    ) {
        _hub = hub;
        _saveService = saveService;
        _logger = logger;
    }

    public void Tick(float delta, DataStore store)
    {
        foreach ((Uuid clientId, _) in _hub.Receive<SaveLevelRequest>())
        {
            //  Capture on the server thread is required (the store is owned by it); the KV writes are
            //  offloaded inside QueueSave.
            _saveService.QueueSave(store);
            Result send = _hub.Send(clientId, new SaveLevelResponse { Success = true });
            if (!send.Success)
            {
                _logger.LogWarning("Failed to send world-management response to client {clientId}: {message}.", clientId, send.Message);
            }
        }
    }
}