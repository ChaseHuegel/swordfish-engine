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
    private readonly ILevelCatalog _levelCatalog = levelCatalog;
    private readonly PermissionResolver _resolver = resolver;
    private readonly ILogger _logger = loggerFactory.CreateLogger<ServerLevelManager>();

    public RpcResult<ListLevelsResponse> List(ServerPeer peer)
    {
        try
        {
            Level[] levels = _levelCatalog.ListLevels();
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

    public RpcResult<NewLevelResponse> Create(ServerPeer peer, in NewLevelRequest request)
    {
        if (!CanCreate(peer))
        {
            _logger.LogInformation(
                "Denied level creation for client {clientId} without {permission}.",
                peer.GameId, GamePermissions.LEVEL_CREATE);
            return RpcResult<NewLevelResponse>.Failure(GameRpcErrors.PERMISSION_DENIED, "level creation is not permitted");
        }

        try
        {
            bool success = _levelCatalog.Create(
                request.Name ?? string.Empty,
                request.Seed ?? string.Empty,
                (GameMode)request.GameMode,
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
        return _resolver.Resolve(peer.Claim, peer.IsLocal).HasPermission(GamePermissions.LEVEL_CREATE);
    }
}
