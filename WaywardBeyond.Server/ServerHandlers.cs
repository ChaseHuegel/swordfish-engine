using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NsdRpc;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Permissions;

namespace WaywardBeyond.Server;

/// <summary>
/// The server RPC handlers. Every handler runs on the nsd-rpc dispatch pump, never the server thread.
/// A handler reads the peer and enqueues. No handler touches a <c>DataStore</c> or the level catalog.
/// </summary>
public sealed class ServerHandlers : GameServerApiHandler
{
    private readonly ServerPeers _peers;
    private readonly ServerHostCommands _commands;
    private readonly ILogger<ServerHandlers> _logger;

    public ServerHandlers(ServerPeers peers, ServerHostCommands commands, ILogger<ServerHandlers> logger)
    {
        _peers = peers;
        _commands = commands;
        _logger = logger;
    }

    public override void OnClientHello(ClientHello message, RpcContext context, CancellationToken cancellationToken)
    {
        if (TryGetPeer(in context, out ServerPeer peer))
        {
            peer.Claim = new UserClaim(message.UserId ?? string.Empty);
        }
    }

    public override void OnLeaveGame(LeaveGameRequest message, RpcContext context, CancellationToken cancellationToken)
    {
        if (TryGetPeer(in context, out ServerPeer peer))
        {
            _commands.EnqueueLeave(peer);
        }
    }

    public override void OnClientHeartbeat(ClientHeartbeatMessage message, RpcContext context, CancellationToken cancellationToken)
    {
        if (TryGetPeer(in context, out ServerPeer peer) && peer.World != null)
        {
            peer.Heartbeats.Enqueue(message);
        }
    }

    public override void OnSendSnapshot(WorldSnapshot snapshot, RpcContext context, CancellationToken cancellationToken)
    {
        if (TryGetPeer(in context, out ServerPeer peer) && peer.World != null)
        {
            peer.Snapshots.Enqueue(snapshot);
        }
    }

    public override void OnChat(ChatMessage message, RpcContext context, CancellationToken cancellationToken)
    {
        if (TryGetPeer(in context, out ServerPeer peer) && peer.World != null)
        {
            peer.Chats.Enqueue(message);
        }
    }

    public override ValueTask<RpcResult<ListLevelsResponse>> ListLevelsAsync(
        ListLevelsRequest request,
        RpcContext context,
        CancellationToken cancellationToken
    ) {
        return TryGetPeer(in context, out ServerPeer peer)
            ? new ValueTask<RpcResult<ListLevelsResponse>>(_commands.ListLevelsAsync(peer, cancellationToken))
            : Failed<ListLevelsResponse>();
    }

    public override ValueTask<RpcResult<NewLevelResponse>> CreateLevelAsync(
        NewLevelRequest request,
        RpcContext context,
        CancellationToken cancellationToken
    ) {
        return TryGetPeer(in context, out ServerPeer peer)
            ? new ValueTask<RpcResult<NewLevelResponse>>(_commands.CreateLevelAsync(peer, request, cancellationToken))
            : Failed<NewLevelResponse>();
    }

    public override ValueTask<RpcResult<DeleteLevelResponse>> DeleteLevelAsync(
        DeleteLevelRequest request,
        RpcContext context,
        CancellationToken cancellationToken
    ) {
        return TryGetPeer(in context, out ServerPeer peer)
            ? new ValueTask<RpcResult<DeleteLevelResponse>>(_commands.DeleteLevelAsync(peer, request, cancellationToken))
            : Failed<DeleteLevelResponse>();
    }

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
        peer.SaveCalls.Enqueue(new ServerPeer.SaveLevelCall(completion, cancellationToken));
        return new ValueTask<RpcResult<SaveLevelResponse>>(completion.Task);
    }

    public override ValueTask<RpcResult<JoinAccept>> JoinAsync(
        JoinRequest request,
        RpcContext context,
        CancellationToken cancellationToken
    ) {
        return TryGetPeer(in context, out ServerPeer peer)
            ? new ValueTask<RpcResult<JoinAccept>>(_commands.JoinAsync(peer, request, cancellationToken))
            : Failed<JoinAccept>();
    }

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

        peer.Touch();
        return true;
    }

    private static ValueTask<RpcResult<T>> Failed<T>()
    {
        return new ValueTask<RpcResult<T>>(
            RpcResult<T>.Failure(GameRpcErrors.NOT_JOINED, "the peer is not registered"));
    }
}
