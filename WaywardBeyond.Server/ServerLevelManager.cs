using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Data;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Permissions;

namespace WaywardBeyond.Server;

/// <summary>
/// The server-level level-management facade for menu-time operations: create, list, and delete saved
/// levels. These requests arrive on connections that have not joined a level yet (they live in the
/// pending set), so no per-level system can serve them. Runs once per server tick before level routing.
/// Each reply goes directly to the requesting connection; responses are matched FIFO on the client.
/// Delete requests are handed to <see cref="PendingLevelDeletes"/> because they must run on the server
/// thread, which owns the loaded levels.
/// </summary>
public sealed class ServerLevelManager
{
    private readonly PendingJoins _pendingJoins;
    private readonly PendingLevelDeletes _pendingDeletes;
    private readonly ILevelCatalog _levelCatalog;
    private readonly IPermissionPolicy _policy;
    private readonly ConnectionClaims _claims;
    private readonly ILogger _logger;

    public ServerLevelManager(
        in PendingJoins pendingJoins,
        in PendingLevelDeletes pendingDeletes,
        in ILevelCatalog levelCatalog,
        in IPermissionPolicy policy,
        in ConnectionClaims claims,
        ILoggerFactory loggerFactory
    ) {
        _pendingJoins = pendingJoins;
        _pendingDeletes = pendingDeletes;
        _levelCatalog = levelCatalog;
        _policy = policy;
        _claims = claims;
        _logger = loggerFactory.CreateLogger<ServerLevelManager>();
    }

    /// <summary>Drains menu-time level-management requests from pending connections. Runs on the server thread.</summary>
    public void Tick()
    {
        foreach (IServerConnection connection in _pendingJoins.Snapshot())
        {
            //  Bind the connection's claim before menu requests resolve permissions this tick.
            Result<ClientHello> hello;
            while ((hello = connection.Receive<ClientHello>()).Success)
            {
                _claims.Bind(connection, new UserClaim(hello.Value.UserId ?? string.Empty));
            }

            Result<NewLevelRequest> newLevel;
            while ((newLevel = connection.Receive<NewLevelRequest>()).Success)
            {
                NewLevelRequest request = newLevel.Value;
                _ = Task.Run(() => HandleNewLevel(connection, request));
            }

            Result<ListLevelsRequest> listLevels;
            while ((listLevels = connection.Receive<ListLevelsRequest>()).Success)
            {
                _ = Task.Run(() => HandleListLevels(connection));
            }

            Result<DeleteLevelRequest> deleteLevel;
            while ((deleteLevel = connection.Receive<DeleteLevelRequest>()).Success)
            {
                DeleteLevelRequest request = deleteLevel.Value;
                _pendingDeletes.Enqueue(connection, request.LevelGuid ?? string.Empty);
            }
        }
    }

    private void SendOrLog<T>(IServerConnection connection, in T message)
    {
        Result send = connection.Send(message);
        if (!send.Success)
        {
            _logger.LogWarning("Failed to send level-management response: {message}.", send.Message);
        }
    }

    private bool CanCreate(IServerConnection connection)
    {
        if (connection.IsLocal)
        {
            return true;
        }

        return _claims.TryGet(connection, out UserClaim claim)
            && _policy.HasPermission(claim.UserId, GamePermissions.LevelCreate);
    }

    private void HandleNewLevel(IServerConnection connection, in NewLevelRequest request)
    {
        if (!CanCreate(connection))
        {
            _logger.LogInformation("Denied level creation for a connection without {permission}.", GamePermissions.LevelCreate);
            SendOrLog(connection, new NewLevelResponse { LevelGuid = string.Empty, Success = false });
            return;
        }

        try
        {
            bool success = _levelCatalog.Create(
                request.Name ?? string.Empty,
                request.Seed ?? string.Empty,
                (GameMode)request.GameMode,
                out string levelGuid
            );
            SendOrLog(connection, new NewLevelResponse { LevelGuid = levelGuid, Success = success });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create level \"{name}\" on behalf of a client.", request.Name);
            SendOrLog(connection, new NewLevelResponse { LevelGuid = string.Empty, Success = false });
        }
    }

    private void HandleListLevels(IServerConnection connection)
    {
        try
        {
            Level[] levels = _levelCatalog.ListLevels();
            SendOrLog(connection, new ListLevelsResponse { Levels = levels, CanCreateSave = CanCreate(connection) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list levels for a client.");
            SendOrLog(connection, new ListLevelsResponse { Levels = [] });
        }
    }
}
