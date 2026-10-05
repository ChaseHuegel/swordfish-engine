using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Shared.Bricks;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core;

/// <summary>
/// The server-level world-management facade for menu-time operations: create, list, and delete saved
/// worlds. These requests arrive on connections that have not joined any world yet (they live in the
/// pending set), so no per-world system can serve them. Runs once per server tick before world routing.
/// Each reply goes directly to the requesting connection; responses are matched FIFO on the client.
/// </summary>
public sealed class ServerWorldManager
{
    private readonly PendingJoins _pendingJoins;
    private readonly WorldSaveService _worldService;
    private readonly ILogger _logger;

    public ServerWorldManager(
        in PendingJoins pendingJoins,
        in Func<KeyValueStore> keyValueStore,
        IBrickIdMap brickIdMap,
        ILoggerFactory loggerFactory
    ) {
        _pendingJoins = pendingJoins;
        _worldService = new WorldSaveService(loggerFactory.CreateLogger<WorldSaveService>(), keyValueStore, brickIdMap);
        _logger = loggerFactory.CreateLogger<ServerWorldManager>();
    }

    /// <summary>Drains menu-time world-management requests from pending connections. Run once per server tick.</summary>
    public void Tick()
    {
        foreach (IServerConnection connection in _pendingJoins.Snapshot())
        {
            Result<NewWorldRequest> newWorld;
            while ((newWorld = connection.Receive<NewWorldRequest>()).Success)
            {
                NewWorldRequest request = newWorld.Value;
                _ = Task.Run(() => HandleNewWorld(connection, request));
            }

            Result<ListWorldsRequest> listWorlds;
            while ((listWorlds = connection.Receive<ListWorldsRequest>()).Success)
            {
                _ = Task.Run(() => HandleListWorlds(connection));
            }

            Result<DeleteWorldRequest> deleteWorld;
            while ((deleteWorld = connection.Receive<DeleteWorldRequest>()).Success)
            {
                DeleteWorldRequest request = deleteWorld.Value;
                _ = Task.Run(() => HandleDeleteWorld(connection, request));
            }
        }
    }

    private void SendOrLog<T>(IServerConnection connection, in T message)
    {
        Result send = connection.Send(message);
        if (!send.Success)
        {
            _logger.LogWarning("Failed to send world-management response: {message}.", send.Message);
        }
    }

    private void HandleNewWorld(IServerConnection connection, in NewWorldRequest request)
    {
        try
        {
            bool success = _worldService.CreateWorld(
                request.Name ?? string.Empty,
                request.Seed ?? string.Empty,
                (GameMode)request.GameMode,
                out string levelGuid
            );
            SendOrLog(connection, new NewWorldResponse { LevelGuid = levelGuid, Success = success });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create world \"{name}\" on behalf of a client.", request.Name);
            SendOrLog(connection, new NewWorldResponse { LevelGuid = string.Empty, Success = false });
        }
    }

    private void HandleListWorlds(IServerConnection connection)
    {
        try
        {
            Level[] levels = _worldService.ListLevels();
            SendOrLog(connection, new ListWorldsResponse { Levels = levels });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list worlds for a client.");
            SendOrLog(connection, new ListWorldsResponse { Levels = [] });
        }
    }

    private void HandleDeleteWorld(IServerConnection connection, in DeleteWorldRequest request)
    {
        try
        {
            _worldService.DeleteLevel(request.LevelGuid ?? string.Empty);
            SendOrLog(connection, new DeleteWorldResponse { Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete world \"{level}\" on behalf of a client.", request.LevelGuid);
            SendOrLog(connection, new DeleteWorldResponse { Success = false });
        }
    }
}