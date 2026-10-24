using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NsdRpc;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Permissions;

namespace WaywardBeyond.Server;

/// <summary>
/// The server RPC handlers. Every handler runs on the nsd-rpc dispatch pump, never the server thread.
/// A handler reads the peer and enqueues. No handler touches a <c>DataStore</c> or the level catalog.
/// Inbound traffic is budgeted per peer: a client that exceeds its rate or stages more than one tick
/// of work is closed, because a reliable delta stream cannot be repaired by dropping messages.
/// </summary>
public sealed class ServerHandlers : GameServerApiHandler
{
    private const int MAX_USER_ID_LENGTH = 128;

    private readonly ServerPeers _peers;
    private readonly ServerHostCommands _commands;
    private readonly ILogger<ServerHandlers> _logger;
    private readonly int _inboundRatePerSecond;
    private readonly int _inboundBurst;
    private readonly int _inboundQueueDepth;

    /// <summary>Creates the handler set with the per-peer inbound budget from the protocol config.</summary>
    public ServerHandlers(ServerPeers peers, ServerHostCommands commands, NetworkingConfig config, ILogger<ServerHandlers> logger)
    {
        _peers = peers;
        _commands = commands;
        _logger = logger;
        _inboundRatePerSecond = Math.Max(1, config.Protocol.InboundRatePerSecond.Get());
        _inboundBurst = Math.Max(1, config.Protocol.InboundBurst.Get());
        _inboundQueueDepth = Math.Max(1, config.Protocol.InboundQueueDepth.Get());
    }

    /// <inheritdoc/>
    public override void OnClientHello(ClientHello message, RpcContext context, CancellationToken cancellationToken)
    {
        if (!TryGetPeer(in context, out ServerPeer peer))
        {
            return;
        }

        //  The claim decides every permission check, so it is fixed before a world binds it.
        if (peer.World != null)
        {
            _logger.LogWarning("Ignored a ClientHello from client {clientId}: the client already joined a world.", peer.GameId);
            return;
        }

        string userId = message.UserId?.Trim() ?? string.Empty;
        if (userId.Length > MAX_USER_ID_LENGTH)
        {
            _logger.LogWarning(
                "Ignored a ClientHello from client {clientId}: the user id exceeds {limit} characters.",
                peer.GameId, MAX_USER_ID_LENGTH);
            return;
        }

        peer.Claim = new UserClaim(userId);
    }

    /// <inheritdoc/>
    public override void OnLeaveGame(LeaveGameRequest message, RpcContext context, CancellationToken cancellationToken)
    {
        if (TryGetPeer(in context, out ServerPeer peer))
        {
            _commands.EnqueueLeave(peer);
        }
    }

    /// <inheritdoc/>
    public override void OnClientHeartbeat(ClientHeartbeatMessage message, RpcContext context, CancellationToken cancellationToken)
    {
        if (TryGetPeer(in context, out ServerPeer peer) && peer.World != null)
        {
            TryStage(peer, peer.Heartbeats, message);
        }
    }

    /// <inheritdoc/>
    public override void OnSendSnapshot(WorldSnapshot snapshot, RpcContext context, CancellationToken cancellationToken)
    {
        if (TryGetPeer(in context, out ServerPeer peer) && peer.World != null)
        {
            TryStage(peer, peer.Snapshots, snapshot);
        }
    }

    /// <inheritdoc/>
    public override void OnChat(ChatMessage message, RpcContext context, CancellationToken cancellationToken)
    {
        if (TryGetPeer(in context, out ServerPeer peer) && peer.World != null)
        {
            TryStage(peer, peer.Chats, message);
        }
    }

    /// <inheritdoc/>
    public override ValueTask<RpcResult<ListLevelsResponse>> ListLevelsAsync(
        ListLevelsRequest request,
        RpcContext context,
        CancellationToken cancellationToken
    ) {
        return TryGetPeer(in context, out ServerPeer peer)
            ? new ValueTask<RpcResult<ListLevelsResponse>>(_commands.ListLevelsAsync(peer, cancellationToken))
            : Failed<ListLevelsResponse>();
    }

    /// <inheritdoc/>
    public override ValueTask<RpcResult<NewLevelResponse>> CreateLevelAsync(
        NewLevelRequest request,
        RpcContext context,
        CancellationToken cancellationToken
    ) {
        return TryGetPeer(in context, out ServerPeer peer)
            ? new ValueTask<RpcResult<NewLevelResponse>>(_commands.CreateLevelAsync(peer, request, cancellationToken))
            : Failed<NewLevelResponse>();
    }

    /// <inheritdoc/>
    public override ValueTask<RpcResult<DeleteLevelResponse>> DeleteLevelAsync(
        DeleteLevelRequest request,
        RpcContext context,
        CancellationToken cancellationToken
    ) {
        return TryGetPeer(in context, out ServerPeer peer)
            ? new ValueTask<RpcResult<DeleteLevelResponse>>(_commands.DeleteLevelAsync(peer, request, cancellationToken))
            : Failed<DeleteLevelResponse>();
    }

    /// <inheritdoc/>
    public override ValueTask<RpcResult<SaveLevelResponse>> SaveLevelAsync(
        SaveLevelRequest request,
        RpcContext context,
        CancellationToken cancellationToken
    ) {
        if (!TryGetPeer(in context, out ServerPeer peer) || peer.World == null)
        {
            _logger.LogDebug("Refused a save from a peer without a world.");
            return new ValueTask<RpcResult<SaveLevelResponse>>(
                RpcResult<SaveLevelResponse>.Failure(GameRpcErrors.NOT_JOINED, "the peer has not joined a world"));
        }

        var completion = new TaskCompletionSource<RpcResult<SaveLevelResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (cancellationToken.CanBeCanceled)
        {
            //  The world tick completes the call when it drains the queue; a cancelled or timed-out
            //  caller must not leave the entry waiting for a tick that will answer nobody.
            cancellationToken.Register(() => completion.TrySetResult(
                RpcResult<SaveLevelResponse>.Failure(GameRpcErrors.INVALID_STATE, "the save request was cancelled")));
        }

        if (!TryStage(peer, peer.SaveCalls, new ServerPeer.SaveLevelCall(completion, cancellationToken)))
        {
            return new ValueTask<RpcResult<SaveLevelResponse>>(
                RpcResult<SaveLevelResponse>.Failure(GameRpcErrors.INVALID_STATE, "the server is behind on this client's requests"));
        }

        return new ValueTask<RpcResult<SaveLevelResponse>>(completion.Task);
    }

    /// <inheritdoc/>
    public override ValueTask<RpcResult<JoinAccept>> JoinAsync(
        JoinRequest request,
        RpcContext context,
        CancellationToken cancellationToken
    ) {
        return TryGetPeer(in context, out ServerPeer peer)
            ? new ValueTask<RpcResult<JoinAccept>>(_commands.JoinAsync(peer, request, cancellationToken))
            : Failed<JoinAccept>();
    }

    /// <inheritdoc/>
    public override ValueTask<RpcResult<LevelStreamComplete>> StreamLevelAsync(
        StreamLevelRequest request,
        RpcContext context,
        CancellationToken cancellationToken
    ) {
        return TryGetPeer(in context, out ServerPeer peer)
            ? new ValueTask<RpcResult<LevelStreamComplete>>(
                _commands.StreamLevelAsync(peer, in context, request, cancellationToken))
            : Failed<LevelStreamComplete>();
    }

    private bool TryGetPeer(in RpcContext context, out ServerPeer peer)
    {
        if (!_peers.TryGet(context.Host, context.ClientId, out peer)
            && context.Host is RpcServer server
            && server.GetPeer(context.ClientId) is RpcPeer rpc)
        {
            //  A buffered frame can dispatch before the connect event reaches the host, so the first
            //  handler creates the peer record.
            peer = _peers.GetOrAdd(context.Host, rpc);
        }

        if (peer == null)
        {
            _logger.LogWarning("Dropped an RPC from an unknown client {clientId}.", context.ClientId);
            return false;
        }

        if (!peer.TryConsumeInbound(_inboundRatePerSecond, _inboundBurst))
        {
            RejectPeer(peer, "it exceeded the inbound message rate");
            return false;
        }

        peer.Touch();
        return true;
    }

    /// <summary>
    /// Stages one message for the world tick. A client that stages more than one tick of work is
    /// flooding, so the peer is closed rather than letting an unbounded queue grow.
    /// </summary>
    private bool TryStage<T>(ServerPeer peer, ConcurrentQueue<T> queue, T message)
    {
        if (queue.Count >= _inboundQueueDepth)
        {
            RejectPeer(peer, $"it staged more than {_inboundQueueDepth} messages");
            return false;
        }

        queue.Enqueue(message);
        return true;
    }

    private void RejectPeer(ServerPeer peer, string reason)
    {
        //  One log line per peer: the close is asynchronous and the dispatch pump keeps delivering
        //  until it lands, so an unbounded log here would be the flood.
        if (peer.TryMarkRejectLogged())
        {
            _logger.LogWarning("Closing client {clientId}: {reason}.", peer.GameId, reason);
        }

        peer.Rpc.Close();
    }

    private static ValueTask<RpcResult<T>> Failed<T>()
    {
        return new ValueTask<RpcResult<T>>(
            RpcResult<T>.Failure(GameRpcErrors.NOT_JOINED, "the peer is not registered"));
    }
}
