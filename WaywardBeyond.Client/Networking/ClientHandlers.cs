using System.Threading;
using NsdRpc;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Rpc;

namespace WaywardBeyond.Client.Networking;

/// <summary>
/// The client RPC handlers. Every handler runs on the nsd-rpc receive pump and only enqueues into
/// <see cref="ClientInbound"/>. No handler touches an ECS store.
/// </summary>
internal sealed class ClientHandlers(ClientInbound inbound) : GameClientApiHandler
{
    private readonly ClientInbound _inbound = inbound;

    public override void OnWorldSnapshot(WorldSnapshot snapshot, RpcContext context, CancellationToken cancellationToken)
    {
        _inbound.Snapshots.Enqueue(new ClientInbound.InboundSnapshot(snapshot));
    }

    public override void OnSnapshotBatch(SnapshotBatch batch, RpcContext context, CancellationToken cancellationToken)
    {
        _inbound.Snapshots.Enqueue(new ClientInbound.InboundSnapshot(batch));
    }

    public override void OnVoxelEdit(VoxelEditMessage message, RpcContext context, CancellationToken cancellationToken)
    {
        _inbound.VoxelEdits.Enqueue(message);
    }

    public override void OnNotification(NotificationMessage message, RpcContext context, CancellationToken cancellationToken)
    {
        _inbound.Notifications.Enqueue(message);
    }

    public override void OnSkillUpdate(SkillStateUpdateMessage message, RpcContext context, CancellationToken cancellationToken)
    {
        _inbound.SkillUpdates.Enqueue(message);
    }

    public override void OnChatRelay(ChatMessage message, RpcContext context, CancellationToken cancellationToken)
    {
        _inbound.Chats.Enqueue(message);
    }

    public override void OnServerHeartbeat(ServerHeartbeatMessage message, RpcContext context, CancellationToken cancellationToken)
    {
        _inbound.Heartbeats.Enqueue(message);
    }
}
