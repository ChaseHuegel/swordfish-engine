using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NsdRpc;
using Swordfish.ECS;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Permissions;

namespace WaywardBeyond.Server;

/// <summary>
/// One connected RPC peer and the game-side state attached to it: the stable game id, the cached
/// client proxy, the player claim, the bound world, and the last inbound stamp. Inbound gameplay
/// messages wait in per-kind queues until the owning world tick drains them on the server thread.
/// </summary>
public sealed class ServerPeer
{
    private readonly object _claimGate = new();
    private UserClaim _claim = UserClaim.Anonymous;
    private long _lastInboundTicks;
    private int _disconnectHandled;

    public ServerPeer(Uuid gameId, RpcPeer rpc, bool isLocal)
    {
        GameId = gameId;
        Rpc = rpc;
        Client = new GameClientApiClient(rpc);
        IsLocal = isLocal;
        Touch();
    }

    /// <summary>The stable game id. Sessions, permissions, and replication key on it.</summary>
    public Uuid GameId { get; }

    /// <summary>The nsd-rpc peer.</summary>
    public RpcPeer Rpc { get; }

    /// <summary>The cached server-to-client proxy.</summary>
    public GameClientApiClient Client { get; }

    /// <summary>True for the in-process loopback peer.</summary>
    public bool IsLocal { get; }

    public EndPoint? RemoteEndPoint => Rpc.RemoteEndPoint;

    public RpcConnectionStats Stats => Rpc.Stats;

    /// <summary>The claim from <c>ClientHello</c>. An absent hello resolves as anonymous.</summary>
    public UserClaim Claim
    {
        get
        {
            lock (_claimGate)
            {
                return _claim;
            }
        }
        set
        {
            lock (_claimGate)
            {
                _claim = value;
            }
        }
    }

    /// <summary>The world this peer is bound to, or null while it is in the menu.</summary>
    public ServerWorld? World { get; set; }

    /// <summary>The reason recorded when the peer left. Null while connected.</summary>
    public RpcDisconnectReason? LastDisconnectReason { get; set; }

    /// <summary>The last inbound message time on the monotonic tick clock.</summary>
    public long LastInboundTicks => Interlocked.Read(ref _lastInboundTicks);

    public ConcurrentQueue<WorldSnapshot> Snapshots { get; } = new();

    public ConcurrentQueue<ChatMessage> Chats { get; } = new();

    public ConcurrentQueue<ClientHeartbeatMessage> Heartbeats { get; } = new();

    public ConcurrentQueue<SaveLevelCall> SaveCalls { get; } = new();

    /// <summary>Stamps the last inbound message time. Called for every inbound message.</summary>
    public void Touch()
    {
        Interlocked.Exchange(ref _lastInboundTicks, Environment.TickCount64);
    }

    /// <summary>True exactly once; used to make disconnect handling idempotent.</summary>
    public bool TryMarkDisconnectHandled()
    {
        return Interlocked.Exchange(ref _disconnectHandled, 1) == 0;
    }

    /// <summary>Drops gameplay messages staged for a previous world.</summary>
    public void ClearInbound()
    {
        while (Snapshots.TryDequeue(out _)) { }
        while (Chats.TryDequeue(out _)) { }
        while (Heartbeats.TryDequeue(out _)) { }
        while (SaveCalls.TryDequeue(out SaveLevelCall call))
        {
            call.Completion.TrySetResult(RpcResult<SaveLevelResponse>.Failure(
                GameRpcErrors.INVALID_STATE, "the peer left the world"));
        }
    }

    /// <summary>One in-world save request waiting for the world tick.</summary>
    public readonly struct SaveLevelCall(
        TaskCompletionSource<RpcResult<SaveLevelResponse>> completion,
        CancellationToken cancellationToken
    ) {
        public readonly TaskCompletionSource<RpcResult<SaveLevelResponse>> Completion = completion;
        public readonly CancellationToken CancellationToken = cancellationToken;
    }
}
