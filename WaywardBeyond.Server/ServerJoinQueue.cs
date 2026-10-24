using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NsdRpc;
using Swordfish.ECS;
using WaywardBeyond.Data;

namespace WaywardBeyond.Server;

/// <summary>
/// The per-world inbound queue of joins and disconnect teardowns. The host routes a join here after
/// it binds the peer to the world. The world's join system processes each entry exactly once on the
/// server thread and completes the caller's result.
/// </summary>
public sealed class ServerJoinQueue
{
    private readonly ConcurrentQueue<JoinCall> _joins = new();
    private readonly ConcurrentQueue<Uuid> _disconnects = new();
    private readonly ConcurrentQueue<Uuid> _leaves = new();

    public void Enqueue(
        ServerPeer peer,
        JoinRequest request,
        TaskCompletionSource<RpcResult<JoinAccept>> completion,
        CancellationToken cancellationToken
    ) {
        _joins.Enqueue(new JoinCall(peer, request, completion, cancellationToken));
    }

    public bool TryDequeueJoin(out JoinCall join)
    {
        return _joins.TryDequeue(out join);
    }

    public void EnqueueDisconnect(Uuid gameId)
    {
        _disconnects.Enqueue(gameId);
    }

    public bool TryDequeueDisconnect(out Uuid gameId)
    {
        return _disconnects.TryDequeue(out gameId);
    }

    public void EnqueueLeave(Uuid gameId)
    {
        _leaves.Enqueue(gameId);
    }

    public bool TryDequeueLeave(out Uuid gameId)
    {
        return _leaves.TryDequeue(out gameId);
    }

    /// <summary>One routed join waiting for the world tick.</summary>
    public readonly struct JoinCall(
        ServerPeer peer,
        JoinRequest request,
        TaskCompletionSource<RpcResult<JoinAccept>> completion,
        CancellationToken cancellationToken
    ) {
        public readonly ServerPeer Peer = peer;
        public readonly JoinRequest Request = request;
        public readonly TaskCompletionSource<RpcResult<JoinAccept>> Completion = completion;
        public readonly CancellationToken CancellationToken = cancellationToken;
    }
}
