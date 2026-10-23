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
    [RpcMethod(GameRpc.Methods.WORLD_SNAPSHOT, RpcDirection.ServerToClient)]
    void WorldSnapshot(WorldSnapshot snapshot);

    [RpcMethod(GameRpc.Methods.WORLD_SNAPSHOT, RpcDirection.ServerToClient)]
    void OnWorldSnapshot(WorldSnapshot snapshot, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.SNAPSHOT_BATCH, RpcDirection.ServerToClient)]
    void SnapshotBatch(SnapshotBatch batch);

    [RpcMethod(GameRpc.Methods.SNAPSHOT_BATCH, RpcDirection.ServerToClient)]
    void OnSnapshotBatch(SnapshotBatch batch, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.VOXEL_EDIT, RpcDirection.ServerToClient)]
    void VoxelEdit(VoxelEditMessage message);

    [RpcMethod(GameRpc.Methods.VOXEL_EDIT, RpcDirection.ServerToClient)]
    void OnVoxelEdit(VoxelEditMessage message, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.NOTIFICATION, RpcDirection.ServerToClient)]
    void Notification(NotificationMessage message);

    [RpcMethod(GameRpc.Methods.NOTIFICATION, RpcDirection.ServerToClient)]
    void OnNotification(NotificationMessage message, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.SKILL_UPDATE, RpcDirection.ServerToClient)]
    void SkillUpdate(SkillStateUpdateMessage message);

    [RpcMethod(GameRpc.Methods.SKILL_UPDATE, RpcDirection.ServerToClient)]
    void OnSkillUpdate(SkillStateUpdateMessage message, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.CHAT_RELAY, RpcDirection.ServerToClient)]
    void ChatRelay(ChatMessage message);

    [RpcMethod(GameRpc.Methods.CHAT_RELAY, RpcDirection.ServerToClient)]
    void OnChatRelay(ChatMessage message, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.SERVER_HEARTBEAT, RpcDirection.ServerToClient)]
    void ServerHeartbeat(ServerHeartbeatMessage message);

    [RpcMethod(GameRpc.Methods.SERVER_HEARTBEAT, RpcDirection.ServerToClient)]
    void OnServerHeartbeat(ServerHeartbeatMessage message, RpcContext context, CancellationToken cancellationToken);
}
