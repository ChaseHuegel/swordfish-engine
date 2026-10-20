using System.Threading;
using NsdRpc;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Networking.Rpc;

/// <summary>
/// Every server-to-client notification. Invocation shapes generate <c>GameClientApiClient</c>, which
/// the server uses through each peer. Handler shapes generate <c>GameClientApiHandler</c>, which the
/// client subclasses.
/// </summary>
[RpcService]
public interface IGameClientApi
{
    [RpcMethod(GameRpc.Methods.WorldSnapshot, RpcDirection.ServerToClient)]
    void WorldSnapshot(WorldSnapshot snapshot);

    [RpcMethod(GameRpc.Methods.WorldSnapshot, RpcDirection.ServerToClient)]
    void OnWorldSnapshot(WorldSnapshot snapshot, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.SnapshotBatch, RpcDirection.ServerToClient)]
    void SnapshotBatch(SnapshotBatch batch);

    [RpcMethod(GameRpc.Methods.SnapshotBatch, RpcDirection.ServerToClient)]
    void OnSnapshotBatch(SnapshotBatch batch, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.VoxelEdit, RpcDirection.ServerToClient)]
    void VoxelEdit(VoxelEditMessage message);

    [RpcMethod(GameRpc.Methods.VoxelEdit, RpcDirection.ServerToClient)]
    void OnVoxelEdit(VoxelEditMessage message, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.Notification, RpcDirection.ServerToClient)]
    void Notification(NotificationMessage message);

    [RpcMethod(GameRpc.Methods.Notification, RpcDirection.ServerToClient)]
    void OnNotification(NotificationMessage message, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.SkillUpdate, RpcDirection.ServerToClient)]
    void SkillUpdate(SkillStateUpdateMessage message);

    [RpcMethod(GameRpc.Methods.SkillUpdate, RpcDirection.ServerToClient)]
    void OnSkillUpdate(SkillStateUpdateMessage message, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.ChatRelay, RpcDirection.ServerToClient)]
    void ChatRelay(ChatMessage message);

    [RpcMethod(GameRpc.Methods.ChatRelay, RpcDirection.ServerToClient)]
    void OnChatRelay(ChatMessage message, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.ServerHeartbeat, RpcDirection.ServerToClient)]
    void ServerHeartbeat(ServerHeartbeatMessage message);

    [RpcMethod(GameRpc.Methods.ServerHeartbeat, RpcDirection.ServerToClient)]
    void OnServerHeartbeat(ServerHeartbeatMessage message, RpcContext context, CancellationToken cancellationToken);
}
