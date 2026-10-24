using System;
using Microsoft.Extensions.Logging;
using NsdRpc;
using WaywardBeyond.Data;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;

namespace WaywardBeyond.Server;

/// <summary>
/// The menu-time level-management facade. The server thread calls it for calls from peers that are
/// not in a world yet, so <see cref="ILevelCatalog"/> access stays single-threaded. Responses are
/// correlated by the RPC call, never by request order.
/// </summary>
public sealed class ServerLevelManager(
    ILevelCatalog levelCatalog,
    PermissionResolver resolver,
    ILoggerFactory loggerFactory
)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<ServerLevelManager>();

    /// <summary>Serves the save-listing call. Runs on the catalog worker.</summary>
    public RpcResult<ListLevelsResponse> List(ServerPeer peer)
    {
        try
        {
            Level[] levels = levelCatalog.ListLevels();
            return RpcResult<ListLevelsResponse>.Success(new ListLevelsResponse
            {
                Levels = levels,
                CanCreateSave = CanCreate(peer),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list levels for client {clientId}.", peer.GameId);
            return RpcResult<ListLevelsResponse>.Failure(GameRpcErrors.LEVEL_LOAD_FAILED, "the level list did not load");
        }
    }

    /// <summary>Serves the create call after the permission and game-mode checks. Runs on the catalog worker.</summary>
    public RpcResult<NewLevelResponse> Create(ServerPeer peer, in NewLevelRequest request)
    {
        if (!CanCreate(peer))
        {
            _logger.LogInformation(
                "Denied level creation for client {clientId} without {permission}.",
                peer.GameId, GamePermissions.LEVEL_CREATE);
            return RpcResult<NewLevelResponse>.Failure(GameRpcErrors.PERMISSION_DENIED, "level creation is not permitted");
        }

        if (!Enum.IsDefined(request.GameMode))
        {
            return RpcResult<NewLevelResponse>.Failure(GameRpcErrors.INVALID_STATE, "the game mode is invalid");
        }

        try
        {
            bool success = levelCatalog.Create(
                request.Name ?? string.Empty,
                request.Seed ?? string.Empty,
                request.GameMode,
                out string levelGuid
            );

            return RpcResult<NewLevelResponse>.Success(new NewLevelResponse
            {
                LevelGuid = levelGuid,
                Success = success,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create level \"{name}\" for client {clientId}.", request.Name, peer.GameId);
            return RpcResult<NewLevelResponse>.Failure(GameRpcErrors.LEVEL_LOAD_FAILED, "the level was not created");
        }
    }

    /// <summary>True when the peer may create a level. A host always may.</summary>
    public bool CanCreate(ServerPeer peer)
    {
        return resolver.Resolve(peer.Claim, peer.IsLocal).HasPermission(GamePermissions.LEVEL_CREATE);
    }

    /// <summary>True when the peer may delete a level. A host always may.</summary>
    public bool CanDelete(ServerPeer peer)
    {
        return resolver.Resolve(peer.Claim, peer.IsLocal).HasPermission(GamePermissions.LEVEL_DELETE);
    }

    /// <summary>Deletes a level from the catalog. Runs off the server tick thread.</summary>
    public bool Delete(string levelGuid)
    {
        try
        {
            return levelCatalog.Delete(levelGuid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete level {level}.", levelGuid);
            return false;
        }
    }
}
