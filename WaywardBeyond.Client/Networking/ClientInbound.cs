using System.Collections.Concurrent;
using NsdRpc;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Client.Networking;

/// <summary>
/// Inbound client queues. The RPC handlers run on the receive pump and only enqueue. The ECS thread
/// drains each queue. The snapshot stream is one ordered queue so a full snapshot and the fragments
/// of a later batch cannot invert. <see cref="Clear"/> runs on every connection switch so no stale
/// frame reaches a system after a reconnect.
/// </summary>
internal sealed class ClientInbound : IChunkSink<LevelStreamChunk>
{
    /// <summary>The ordered snapshot stream: full snapshots and fragment batches in arrival order.</summary>
    public ConcurrentQueue<InboundSnapshot> Snapshots { get; } = new();

    public ConcurrentQueue<ServerHeartbeatMessage> Heartbeats { get; } = new();

    public ConcurrentQueue<VoxelEditMessage> VoxelEdits { get; } = new();

    public ConcurrentQueue<NotificationMessage> Notifications { get; } = new();

    public ConcurrentQueue<SkillStateUpdateMessage> SkillUpdates { get; } = new();

    public ConcurrentQueue<ChatMessage> Chats { get; } = new();

    public ConcurrentQueue<LevelStreamChunk> LevelChunks { get; } = new();

    public void OnChunk(in LevelStreamChunk chunk)
    {
        LevelChunks.Enqueue(chunk);
    }

    public void Clear()
    {
        while (Snapshots.TryDequeue(out _)) { }
        while (Heartbeats.TryDequeue(out _)) { }
        while (VoxelEdits.TryDequeue(out _)) { }
        while (Notifications.TryDequeue(out _)) { }
        while (SkillUpdates.TryDequeue(out _)) { }
        while (Chats.TryDequeue(out _)) { }
        while (LevelChunks.TryDequeue(out _)) { }
    }

    /// <summary>One entry of the ordered snapshot stream.</summary>
    public readonly struct InboundSnapshot
    {
        public readonly WorldSnapshot Full;
        public readonly SnapshotBatch Batch;
        public readonly bool IsBatch;

        public InboundSnapshot(WorldSnapshot full)
        {
            Full = full;
            Batch = default;
            IsBatch = false;
        }

        public InboundSnapshot(SnapshotBatch batch)
        {
            Full = default;
            Batch = batch;
            IsBatch = true;
        }
    }
}
