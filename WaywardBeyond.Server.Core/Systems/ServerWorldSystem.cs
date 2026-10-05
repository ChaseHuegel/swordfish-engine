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
/// Handles the world-level save request: the player's pause-menu save queues an authoritative flush of
/// the world through its <see cref="WorldSaveService"/> and replies to the requesting client. Menu-time
/// world management (create/list/delete) is served by <see cref="ServerWorldManager"/> instead, because
/// those requests arrive on connections that have not joined a world yet.
/// </summary>
public sealed class ServerWorldSystem : IServerWorldSystem
{
    private readonly ServerConnectionHub _hub;
    private readonly WorldSaveService _worldService;
    private readonly ILogger<ServerWorldSystem> _logger;

    public ServerWorldSystem(
        in ServerConnectionHub hub,
        in WorldSaveService worldService,
        in ILogger<ServerWorldSystem> logger
    ) {
        _hub = hub;
        _worldService = worldService;
        _logger = logger;
    }

    public void Tick(float delta, DataStore store)
    {
        foreach ((Uuid clientId, _) in _hub.Receive<SaveWorldRequest>())
        {
            //  Capture on the server thread is required (the store is owned by it); the KV writes are
            //  offloaded inside QueueWorldSave.
            _worldService.QueueWorldSave(store);
            Result send = _hub.Send(clientId, new SaveWorldResponse { Success = true });
            if (!send.Success)
            {
                _logger.LogWarning("Failed to send world-management response to client {clientId}: {message}.", clientId, send.Message);
            }
        }
    }
}