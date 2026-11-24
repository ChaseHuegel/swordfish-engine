using System.Threading;
using NsdRpc;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Networking.Rpc;

/// <summary>
/// Every server-to-client notification. Invocation shapes generate <c>GameClientApiClient</c>, which
/// the server uses through each peer. Handler shapes generate <c>GameClientApiHandler</c>, which the
/// client subclasses. A <c>bool</c> invocation returns whether the reliable send path accepted the
/// frame. A handler stays <c>void</c> because it runs locally and has no frame to reject.
/// </summary>
[RpcService]
public interface IGameClientApi
{
    [RpcMethod(GameRpc.Methods.SEND_WORLD_SNAPSHOT, RpcDirection.ServerToClient)]
    bool SendSnapshot(WorldSnapshot snapshot);

    [RpcMethod(GameRpc.Methods.SEND_WORLD_SNAPSHOT, RpcDirection.ServerToClient)]
    void OnSendSnapshot(WorldSnapshot snapshot, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.SNAPSHOT_BATCH, RpcDirection.ServerToClient)]
    bool SnapshotBatch(SnapshotBatch batch);

    [RpcMethod(GameRpc.Methods.SNAPSHOT_BATCH, RpcDirection.ServerToClient)]
    void OnSnapshotBatch(SnapshotBatch batch, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.VOXEL_EDIT, RpcDirection.ServerToClient)]
    bool VoxelEdit(VoxelEditMessage message);

    [RpcMethod(GameRpc.Methods.VOXEL_EDIT, RpcDirection.ServerToClient)]
    void OnVoxelEdit(VoxelEditMessage message, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.VOXEL_FACE_EDIT, RpcDirection.ServerToClient)]
    bool VoxelFaceEdit(VoxelFaceEditMessage message);

    [RpcMethod(GameRpc.Methods.VOXEL_FACE_EDIT, RpcDirection.ServerToClient)]
    void OnVoxelFaceEdit(VoxelFaceEditMessage message, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.NOTIFICATION, RpcDirection.ServerToClient)]
    bool Notification(NotificationMessage message);

    [RpcMethod(GameRpc.Methods.NOTIFICATION, RpcDirection.ServerToClient)]
    void OnNotification(NotificationMessage message, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.SKILL_UPDATE, RpcDirection.ServerToClient)]
    bool SkillUpdate(SkillStateUpdateMessage message);

    [RpcMethod(GameRpc.Methods.SKILL_UPDATE, RpcDirection.ServerToClient)]
    void OnSkillUpdate(SkillStateUpdateMessage message, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.CHAT_RELAY, RpcDirection.ServerToClient)]
    bool ChatRelay(ChatMessage message);

    [RpcMethod(GameRpc.Methods.CHAT_RELAY, RpcDirection.ServerToClient)]
    void OnChatRelay(ChatMessage message, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.SERVER_HEARTBEAT, RpcDirection.ServerToClient)]
    bool ServerHeartbeat(ServerHeartbeatMessage message);

    [RpcMethod(GameRpc.Methods.SERVER_HEARTBEAT, RpcDirection.ServerToClient)]
    void OnServerHeartbeat(ServerHeartbeatMessage message, RpcContext context, CancellationToken cancellationToken);
}
