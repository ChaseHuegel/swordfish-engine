using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NsdRpc;
using Swordfish.ECS;
using WaywardBeyond.Bricks;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Server;

/// <summary>
/// The host-level command processor. Handlers enqueue here from the RPC pump. The server thread
/// drains the queue each tick: it binds claims, routes joins and leaves, serves menu calls, and paces
/// the level stream. It owns every <c>ILevelCatalog</c> call, so catalog access stays single-threaded.
/// </summary>
public sealed class ServerHostCommands(
    ServerLevelManager levelManager,
    IBrickRegistry brickRegistry,
    NetworkingConfig config,
    ILogger<ServerHostCommands> logger
) {
    private readonly ConcurrentQueue<HostCall> _calls = new();
    private readonly List<ActiveStream> _streams = [];

    /// <summary>Drains host calls and advances every active level stream. Called on the server thread.</summary>
    public void Tick(ServerWorldHost host)
    {
        while (_calls.TryDequeue(out HostCall call))
        {
            try
            {
                call.Execute(this, host);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while processing a host RPC command.");
            }
        }

        TickStreams();
    }

    public void EnqueueLeave(ServerPeer peer)
    {
        _calls.Enqueue(new LeaveCall(peer));
    }

    public Task<RpcResult<ListLevelsResponse>> ListLevelsAsync(ServerPeer peer, CancellationToken cancellationToken)
    {
        var completion = NewCompletion<ListLevelsResponse>();
        RegisterCancellation(cancellationToken, completion);
        _calls.Enqueue(new ListLevelsCall(peer, completion, cancellationToken));
        return completion.Task;
    }

    public Task<RpcResult<NewLevelResponse>> CreateLevelAsync(
        ServerPeer peer,
        NewLevelRequest request,
        CancellationToken cancellationToken
    ) {
        var completion = NewCompletion<NewLevelResponse>();
        RegisterCancellation(cancellationToken, completion);
        _calls.Enqueue(new CreateLevelCall(peer, request, completion, cancellationToken));
        return completion.Task;
    }

    public Task<RpcResult<DeleteLevelResponse>> DeleteLevelAsync(
        ServerPeer peer,
        DeleteLevelRequest request,
        CancellationToken cancellationToken
    ) {
        var completion = NewCompletion<DeleteLevelResponse>();
        RegisterCancellation(cancellationToken, completion);
        _calls.Enqueue(new DeleteLevelCall(peer, request, completion, cancellationToken));
        return completion.Task;
    }

    public Task<RpcResult<JoinAccept>> JoinAsync(
        ServerPeer peer,
        JoinRequest request,
        CancellationToken cancellationToken
    ) {
        var completion = NewCompletion<JoinAccept>();
        RegisterCancellation(cancellationToken, completion);
        _calls.Enqueue(new JoinCall(peer, request, completion, cancellationToken));
        return completion.Task;
    }

    public Task<RpcResult<LevelStreamComplete>> StreamLevelAsync(
        ServerPeer peer,
        in RpcContext context,
        StreamLevelRequest request,
        CancellationToken cancellationToken
    ) {
        var completion = NewCompletion<LevelStreamComplete>();
        RegisterCancellation(cancellationToken, completion);
        _calls.Enqueue(new StreamCall(peer, context, request, completion, cancellationToken));
        return completion.Task;
    }

    private void HandleJoin(ServerWorldHost host, in JoinCall call)
    {
        if (call.CancellationToken.IsCancellationRequested)
        {
            call.Completion.TrySetResult(RpcResult<JoinAccept>.Failure(GameRpcErrors.INVALID_STATE, "join cancelled"));
            return;
        }

        try
        {
            host.RouteJoin(call.Peer, call.Request, call.Completion, call.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to route a join for client {clientId}.", call.Peer.GameId);
            call.Completion.TrySetResult(RpcResult<JoinAccept>.Failure(
                GameRpcErrors.JOIN_REFUSED, "the world could not be created"));
        }
    }

    private void HandleLeave(ServerPeer peer)
    {
        peer.World?.JoinQueue.EnqueueLeave(peer.GameId);
    }

    private void HandleListLevels(in ListLevelsCall call)
    {
        if (call.CancellationToken.IsCancellationRequested)
        {
            call.Completion.TrySetResult(RpcResult<ListLevelsResponse>.Failure(GameRpcErrors.INVALID_STATE, "cancelled"));
            return;
        }

        call.Completion.TrySetResult(levelManager.List(call.Peer));
    }

    private void HandleCreateLevel(in CreateLevelCall call)
    {
        if (call.CancellationToken.IsCancellationRequested)
        {
            call.Completion.TrySetResult(RpcResult<NewLevelResponse>.Failure(GameRpcErrors.INVALID_STATE, "cancelled"));
            return;
        }

        call.Completion.TrySetResult(levelManager.Create(call.Peer, call.Request));
    }

    private void HandleDeleteLevel(ServerWorldHost host, in DeleteLevelCall call)
    {
        if (call.CancellationToken.IsCancellationRequested)
        {
            call.Completion.TrySetResult(RpcResult<DeleteLevelResponse>.Failure(GameRpcErrors.INVALID_STATE, "cancelled"));
            return;
        }

        if (!levelManager.CanDelete(call.Peer))
        {
            logger.LogInformation(
                "Denied level deletion for client {clientId} without {permission}.",
                call.Peer.GameId, GamePermissions.LEVEL_DELETE);
            call.Completion.TrySetResult(RpcResult<DeleteLevelResponse>.Failure(
                GameRpcErrors.PERMISSION_DENIED, "level deletion is not permitted"));
            return;
        }

        try
        {
            bool success = host.DeleteLevel(call.Request.LevelGuid ?? string.Empty);
            call.Completion.TrySetResult(RpcResult<DeleteLevelResponse>.Success(new DeleteLevelResponse { Success = success }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete level {level}.", call.Request.LevelGuid);
            call.Completion.TrySetResult(RpcResult<DeleteLevelResponse>.Failure(
                GameRpcErrors.LEVEL_LOAD_FAILED, "the level was not deleted"));
        }
    }

    private void HandleStream(in StreamCall call)
    {
        if (call.CancellationToken.IsCancellationRequested)
        {
            call.Completion.TrySetResult(RpcResult<LevelStreamComplete>.Failure(GameRpcErrors.INVALID_STATE, "cancelled"));
            return;
        }

        ServerWorld? world = call.Peer.World;
        if (world == null || !world.Sessions.TryGetEntity(call.Peer.GameId, out _))
        {
            call.Completion.TrySetResult(RpcResult<LevelStreamComplete>.Failure(
                GameRpcErrors.NOT_JOINED, "the peer has not joined a world"));
            return;
        }

        if (FindStream(call.Peer) != null)
        {
            call.Completion.TrySetResult(RpcResult<LevelStreamComplete>.Failure(
                GameRpcErrors.INVALID_STATE, "a level stream is already in flight"));
            return;
        }

        var entities = new List<int>();
        try
        {
            var collect = new CollectLevelEntitiesAction(entities);
            world.Store.Query<VoxelWorldComponent, NetworkComponent, TransformComponent, CollectLevelEntitiesAction>(0f, ref collect);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to collect the level entities for client {clientId}.", call.Peer.GameId);
            call.Completion.TrySetResult(RpcResult<LevelStreamComplete>.Failure(
                GameRpcErrors.STREAM_FAILED, "the level entities did not load"));
            return;
        }

        _streams.Add(new ActiveStream(call.Peer, call.Context, call.Completion, call.CancellationToken, world, entities));
        logger.LogDebug("Streaming {count} level entities to client {clientId}.", entities.Count, call.Peer.GameId);
    }

    private void TickStreams()
    {
        int budget = Math.Max(1, config.Protocol.LevelStreamBytesPerTick.Get());
        int backlogBytes = config.Transport.SendBacklogDisconnectBytes.Get();
        int watermark = backlogBytes > 0
            ? backlogBytes
            : Math.Max(1, config.Transport.SendQueueBytes.Get() / 2);

        for (int index = _streams.Count - 1; index >= 0; index--)
        {
            ActiveStream stream = _streams[index];
            try
            {
                if (!Advance(stream, budget, watermark))
                {
                    continue;
                }

                _streams.RemoveAt(index);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Level stream to client {clientId} failed.", stream.Peer.GameId);
                Abort(stream, GameRpcErrors.STREAM_FAILED, "the stream failed");
                _streams.RemoveAt(index);
            }
        }
    }

    /// <summary>Advances one stream. Returns true when it completed and left the active list.</summary>
    private bool Advance(ActiveStream stream, int budget, int watermark)
    {
        ServerPeer peer = stream.Peer;
        if (peer.World != stream.World)
        {
            Complete(stream, RpcResult<LevelStreamComplete>.Failure(GameRpcErrors.NOT_JOINED, "the peer left the world"));
            return true;
        }

        if (stream.CancellationToken.IsCancellationRequested)
        {
            Complete(stream, RpcResult<LevelStreamComplete>.Failure(GameRpcErrors.INVALID_STATE, "cancelled"));
            return true;
        }

        int sent = 0;
        while (true)
        {
            if (stream.Payload == null)
            {
                if (stream.EntityIndex >= stream.Entities.Count)
                {
                    Complete(stream, RpcResult<LevelStreamComplete>.Success(new LevelStreamComplete()));
                    return true;
                }

                if (!TryEncode(stream, out int payloadBytes))
                {
                    stream.EntityIndex++;
                    continue;
                }

                stream.PartCount = PayloadFragmenter.PartCount(payloadBytes);
                stream.PartIndex = 0;
            }

            if (sent >= budget || peer.Stats.QueuedBytes >= watermark)
            {
                return false;
            }

            byte[] part = PayloadFragmenter.Slice(stream.Payload, stream.PartIndex);
            sent += part.Length;

            var chunk = new LevelStreamChunk
            {
                PartIndex = (uint)stream.PartIndex,
                PartCount = (uint)stream.PartCount,
                Payload = part,
            };

            if (!stream.Context.TryStream(chunk))
            {
                logger.LogError("Level stream to client {clientId} was rejected; closing the peer.", peer.GameId);
                Abort(stream, GameRpcErrors.STREAM_FAILED, "the stream send was rejected");
                peer.Rpc.Close();
                return true;
            }

            stream.PartIndex++;
            if (stream.PartIndex >= stream.PartCount)
            {
                stream.EntityIndex++;
                stream.Payload = null;
            }
        }
    }

    private bool TryEncode(ActiveStream stream, out int payloadBytes)
    {
        payloadBytes = 0;
        if (stream.EntityIndex >= stream.Entities.Count)
        {
            return false;
        }

        int entity = stream.Entities[stream.EntityIndex];
        VoxelEntityData data = VoxelWorldEntityFactory.ToVoxelEntityData(stream.World.Store, entity);
        if (data.Uuid == 0)
        {
            return false;
        }

        //  Attach the brick palette so the client can resolve the server's registry ids locally.
        stream.Payload = VoxelCodec.Encode(data, brickRegistry).Serialize();
        payloadBytes = stream.Payload.Length;
        return true;
    }

    private void Complete(ActiveStream stream, RpcResult<LevelStreamComplete> result)
    {
        stream.World.Replication.EndStream(stream.Peer.GameId);
        stream.Completion.TrySetResult(result);
    }

    private void Abort(ActiveStream stream, int errorCode, string message)
    {
        stream.World.Replication.EndStream(stream.Peer.GameId);
        stream.Completion.TrySetResult(RpcResult<LevelStreamComplete>.Failure(errorCode, message));
    }

    private ActiveStream? FindStream(ServerPeer peer)
    {
        foreach (ActiveStream stream in _streams)
        {
            if (stream.Peer == peer)
            {
                return stream;
            }
        }

        return null;
    }

    private static TaskCompletionSource<RpcResult<T>> NewCompletion<T>()
    {
        return new TaskCompletionSource<RpcResult<T>>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static void RegisterCancellation<T>(CancellationToken token, TaskCompletionSource<RpcResult<T>> completion)
    {
        if (!token.CanBeCanceled)
        {
            return;
        }

        token.Register(() => completion.TrySetResult(RpcResult<T>.Failure(GameRpcErrors.INVALID_STATE, "cancelled")));
    }

    private abstract class HostCall
    {
        public abstract void Execute(ServerHostCommands owner, ServerWorldHost host);
    }

    private sealed class LeaveCall(ServerPeer peer) : HostCall
    {
        public override void Execute(ServerHostCommands owner, ServerWorldHost host)
        {
            owner.HandleLeave(peer);
        }
    }

    private sealed class ListLevelsCall(
        ServerPeer peer,
        TaskCompletionSource<RpcResult<ListLevelsResponse>> completion,
        CancellationToken cancellationToken
    ) : HostCall
    {
        public ServerPeer Peer { get; } = peer;
        public TaskCompletionSource<RpcResult<ListLevelsResponse>> Completion { get; } = completion;
        public CancellationToken CancellationToken { get; } = cancellationToken;

        public override void Execute(ServerHostCommands owner, ServerWorldHost host)
        {
            owner.HandleListLevels(this);
        }
    }

    private sealed class CreateLevelCall(
        ServerPeer peer,
        NewLevelRequest request,
        TaskCompletionSource<RpcResult<NewLevelResponse>> completion,
        CancellationToken cancellationToken
    ) : HostCall
    {
        public ServerPeer Peer { get; } = peer;
        public NewLevelRequest Request { get; } = request;
        public TaskCompletionSource<RpcResult<NewLevelResponse>> Completion { get; } = completion;
        public CancellationToken CancellationToken { get; } = cancellationToken;

        public override void Execute(ServerHostCommands owner, ServerWorldHost host)
        {
            owner.HandleCreateLevel(this);
        }
    }

    private sealed class DeleteLevelCall(
        ServerPeer peer,
        DeleteLevelRequest request,
        TaskCompletionSource<RpcResult<DeleteLevelResponse>> completion,
        CancellationToken cancellationToken
    ) : HostCall
    {
        public ServerPeer Peer { get; } = peer;
        public DeleteLevelRequest Request { get; } = request;
        public TaskCompletionSource<RpcResult<DeleteLevelResponse>> Completion { get; } = completion;
        public CancellationToken CancellationToken { get; } = cancellationToken;

        public override void Execute(ServerHostCommands owner, ServerWorldHost host)
        {
            owner.HandleDeleteLevel(host, this);
        }
    }

    private sealed class JoinCall(
        ServerPeer peer,
        JoinRequest request,
        TaskCompletionSource<RpcResult<JoinAccept>> completion,
        CancellationToken cancellationToken
    ) : HostCall
    {
        public ServerPeer Peer { get; } = peer;
        public JoinRequest Request { get; } = request;
        public TaskCompletionSource<RpcResult<JoinAccept>> Completion { get; } = completion;
        public CancellationToken CancellationToken { get; } = cancellationToken;

        public override void Execute(ServerHostCommands owner, ServerWorldHost host)
        {
            owner.HandleJoin(host, this);
        }
    }

    private sealed class StreamCall(
        ServerPeer peer,
        RpcContext context,
        StreamLevelRequest request,
        TaskCompletionSource<RpcResult<LevelStreamComplete>> completion,
        CancellationToken cancellationToken
    ) : HostCall
    {
        public ServerPeer Peer { get; } = peer;
        public RpcContext Context { get; } = context;
        public StreamLevelRequest Request { get; } = request;
        public TaskCompletionSource<RpcResult<LevelStreamComplete>> Completion { get; } = completion;
        public CancellationToken CancellationToken { get; } = cancellationToken;

        public override void Execute(ServerHostCommands owner, ServerWorldHost host)
        {
            owner.HandleStream(this);
        }
    }

    private sealed class ActiveStream(
        ServerPeer peer,
        RpcContext context,
        TaskCompletionSource<RpcResult<LevelStreamComplete>> completion,
        CancellationToken cancellationToken,
        ServerWorld world,
        List<int> entities
    ) {
        public ServerPeer Peer { get; } = peer;
        public RpcContext Context { get; } = context;
        public TaskCompletionSource<RpcResult<LevelStreamComplete>> Completion { get; } = completion;
        public CancellationToken CancellationToken { get; } = cancellationToken;
        public ServerWorld World { get; } = world;
        public List<int> Entities { get; } = entities;
        public int EntityIndex { get; set; }
        public int PartIndex { get; set; }
        public int PartCount { get; set; }
        public byte[]? Payload { get; set; }
    }

    private readonly struct CollectLevelEntitiesAction(List<int> entities) : IForEach<VoxelWorldComponent, NetworkComponent, TransformComponent>
    {
        public void Execute(float delta, DataStore store, int entity, in VoxelWorldComponent world, in NetworkComponent network, in TransformComponent transform)
        {
            entities.Add(entity);
        }
    }
}
