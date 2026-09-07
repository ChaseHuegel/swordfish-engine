using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core.Systems;

/// <summary>
/// Handles the server-side world-management requests that back the save-listing UI: create a new world
/// (runs shared world generation and persists it), list saved worlds, and delete a saved world. These
/// are menu-time operations that perform blocking KV work and (for creation) full world generation, so
/// each is dispatched to a worker thread and the response is routed back to the requesting client when
/// done - never blocking the server tick loop mid-physics.
/// </summary>
public sealed class ServerWorldSystem : IEntitySystem
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
        foreach ((Uuid clientId, NewWorldRequest request) in _hub.Receive<NewWorldRequest>())
        {
            NewWorldRequest message = request;
            _ = Task.Run(() => HandleNewWorld(clientId, message));
        }

        foreach ((Uuid clientId, _) in _hub.Receive<ListWorldsRequest>())
        {
            _ = Task.Run(() => HandleListWorlds(clientId));
        }

        foreach ((Uuid clientId, DeleteWorldRequest request) in _hub.Receive<DeleteWorldRequest>())
        {
            DeleteWorldRequest message = request;
            _ = Task.Run(() => HandleDeleteWorld(clientId, message));
        }

        foreach ((Uuid clientId, _) in _hub.Receive<SaveWorldRequest>())
        {
            //  Capture on the server thread is required (the store is owned by it); the KV writes are
            //  offloaded inside QueueWorldSave.
            _worldService.QueueWorldSave(store);
            _hub.Send(clientId, new SaveWorldResponse { Success = true });
        }
    }

    private void HandleNewWorld(Uuid clientId, NewWorldRequest request)
    {
        try
        {
            bool success = _worldService.CreateWorld(
                request.Name ?? string.Empty,
                request.Seed ?? string.Empty,
                (GameMode)request.GameMode,
                out string levelGuid
            );
            _hub.Send(clientId, new NewWorldResponse { LevelGuid = levelGuid, Success = success });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create world \"{name}\" on behalf of client {client}.", request.Name, clientId);
            _hub.Send(clientId, new NewWorldResponse { LevelGuid = string.Empty, Success = false });
        }
    }

    private void HandleListWorlds(Uuid clientId)
    {
        try
        {
            Level[] levels = _worldService.ListLevels();
            _hub.Send(clientId, new ListWorldsResponse { Levels = levels });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list worlds for client {client}.", clientId);
            _hub.Send(clientId, new ListWorldsResponse { Levels = [] });
        }
    }

    private void HandleDeleteWorld(Uuid clientId, DeleteWorldRequest request)
    {
        try
        {
            _worldService.DeleteLevel(request.LevelGuid ?? string.Empty);
            _hub.Send(clientId, new DeleteWorldResponse { Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete world \"{level}\" on behalf of client {client}.", request.LevelGuid, clientId);
            _hub.Send(clientId, new DeleteWorldResponse { Success = false });
        }
    }
}