using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NsdRpc;
using Krill;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Permissions;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Server;

/// <summary>
/// One connected RPC peer and the game-side state attached to it: the stable game id, the cached
/// client proxy, the player claim, the bound world, and the last inbound stamp. Inbound gameplay
/// messages wait in per-kind queues until the owning world tick drains them on the server thread.
/// </summary>
public sealed class ServerPeer
{
    private readonly Lock _claimGate = new();
    private readonly Lock _inboundGate = new();
    private UserClaim _claim = UserClaim.Anonymous;
    private long _lastInboundTicks;
    private int _disconnectHandled;
    private double _inboundTokens;
    private long _inboundRefilledMs;
    private long _droppedInbound;
    private int _rejectLogged;

    /// <summary>Creates the game-side record for one RPC client.</summary>
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

    /// <summary>The transport endpoint, or null for the in-process loopback.</summary>
    public EndPoint? RemoteEndPoint => Rpc.RemoteEndPoint;

    /// <summary>The connection counters for this peer.</summary>
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

    /// <summary>Inbound client snapshots staged for the world tick.</summary>
    public ConcurrentQueue<WorldSnapshot> Snapshots { get; } = new();

    /// <summary>Inbound chat messages staged for the world tick.</summary>
    public ConcurrentQueue<ChatMessage> Chats { get; } = new();

    /// <summary>Inbound client heartbeats staged for the world tick.</summary>
    public ConcurrentQueue<ClientHeartbeatMessage> Heartbeats { get; } = new();

    /// <summary>Inbound save requests staged for the world tick.</summary>
    public ConcurrentQueue<SaveLevelCall> SaveCalls { get; } = new();

    /// <summary>Inbound cut region requests staged for the world tick.</summary>
    public ConcurrentQueue<CutRegionCall> CutRegionCalls { get; } = new();

    /// <summary>Inbound fragmented paste region uploads staged for the world tick.</summary>
    public ConcurrentQueue<PasteRegionPartCall> PasteRegionParts { get; } = new();

    /// <summary>Inbound blueprint placeable item requests staged for the world tick.</summary>
    public ConcurrentQueue<PlaceBlueprintItemCall> PlaceBlueprintItemCalls { get; } = new();

    /// <summary>Reassembles multi-part paste region uploads for this peer on the world tick.</summary>
    public FragmentAssembler RegionUploadAssembler { get; } = new(PayloadFragmenter.MAX_PAYLOAD_BYTES);

    /// <summary>Stamps the last inbound message time. Called for every inbound message.</summary>
    public void Touch()
    {
        Interlocked.Exchange(ref _lastInboundTicks, Environment.TickCount64);
    }

    /// <summary>Counts inbound messages this peer dropped for exceeding its rate or queue budget.</summary>
    public long DroppedInbound => Interlocked.Read(ref _droppedInbound);

    /// <summary>
    /// Spends one message from the peer's inbound token budget. The peer's handlers run on one dispatch
    /// pump, so the bucket needs no interlocked read-modify-write; it only guards against a tick race.
    /// </summary>
    public bool TryConsumeInbound(int ratePerSecond, int burst)
    {
        long now = Environment.TickCount64;

        lock (_inboundGate)
        {
            if (_inboundRefilledMs == 0)
            {
                _inboundRefilledMs = now;
                _inboundTokens = burst;
            }

            double elapsedSeconds = (now - _inboundRefilledMs) / 1000d;
            _inboundRefilledMs = now;
            _inboundTokens = Math.Min(burst, _inboundTokens + elapsedSeconds * ratePerSecond);

            if (_inboundTokens < 1)
            {
                Interlocked.Increment(ref _droppedInbound);
                return false;
            }

            _inboundTokens -= 1;
            return true;
        }
    }

    /// <summary>True once per peer; bounds the log to one line while a rejected peer is closing.</summary>
    public bool TryMarkRejectLogged()
    {
        return Interlocked.Exchange(ref _rejectLogged, 1) == 0;
    }

    /// <summary>
    /// Closes the peer after the reliable send path rejected a notification. The delta stream cannot
    /// be repaired by dropping a message, so the peer reconnects to a fresh join.
    /// </summary>
    public void CloseOnRejectedSend(ILogger logger)
    {
        if (TryMarkRejectLogged())
        {
            logger.LogWarning("Closing client {clientId}: the reliable send path rejected a notification.", GameId);
        }

        Rpc.Close();
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

        while (CutRegionCalls.TryDequeue(out CutRegionCall cutCall))
        {
            cutCall.Completion.TrySetResult(RpcResult<RegionOpResponse>.Failure(
                GameRpcErrors.INVALID_STATE, "the peer left the world"));
        }

        while (PasteRegionParts.TryDequeue(out PasteRegionPartCall pasteCall))
        {
            pasteCall.Completion.TrySetResult(RpcResult<RegionOpResponse>.Failure(
                GameRpcErrors.INVALID_STATE, "the peer left the world"));
        }

        while (PlaceBlueprintItemCalls.TryDequeue(out PlaceBlueprintItemCall placeCall))
        {
            placeCall.Completion.TrySetResult(RpcResult<RegionOpResponse>.Failure(
                GameRpcErrors.INVALID_STATE, "the peer left the world"));
        }

        RegionUploadAssembler.Reset();
    }

    /// <summary>One in-world save request waiting for the world tick.</summary>
    public readonly struct SaveLevelCall(
        TaskCompletionSource<RpcResult<SaveLevelResponse>> completion,
        CancellationToken cancellationToken
    ) {
        public readonly TaskCompletionSource<RpcResult<SaveLevelResponse>> Completion = completion;
        public readonly CancellationToken CancellationToken = cancellationToken;
    }

    /// <summary>One in-world cut region request waiting for the world tick.</summary>
    public readonly struct CutRegionCall(
        CutRegionRequest request,
        TaskCompletionSource<RpcResult<RegionOpResponse>> completion,
        CancellationToken cancellationToken
    ) {
        public readonly CutRegionRequest Request = request;
        public readonly TaskCompletionSource<RpcResult<RegionOpResponse>> Completion = completion;
        public readonly CancellationToken CancellationToken = cancellationToken;
    }

    /// <summary>One in-world fragmented paste upload part waiting for the world tick.</summary>
    public readonly struct PasteRegionPartCall(
        RegionUploadPart part,
        TaskCompletionSource<RpcResult<RegionOpResponse>> completion,
        CancellationToken cancellationToken
    ) {
        public readonly RegionUploadPart Part = part;
        public readonly TaskCompletionSource<RpcResult<RegionOpResponse>> Completion = completion;
        public readonly CancellationToken CancellationToken = cancellationToken;
    }

    /// <summary>One in-world blueprint item placement request waiting for the world tick.</summary>
    public readonly struct PlaceBlueprintItemCall(
        PlaceBlueprintItemRequest request,
        TaskCompletionSource<RpcResult<RegionOpResponse>> completion,
        CancellationToken cancellationToken
    ) {
        public readonly PlaceBlueprintItemRequest Request = request;
        public readonly TaskCompletionSource<RpcResult<RegionOpResponse>> Completion = completion;
        public readonly CancellationToken CancellationToken = cancellationToken;
    }
}
