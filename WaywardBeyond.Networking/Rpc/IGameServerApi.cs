using System.Threading;
using System.Threading.Tasks;
using NsdRpc;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Networking.Rpc;

/// <summary>
/// Every client-to-server call, notification, and stream. Invocation shapes (no
/// <see cref="RpcContext"/>) generate <c>GameServerApiClient</c>. Handler shapes (with a context)
/// generate <c>GameServerApiHandler</c>, which the server subclasses.
/// </summary>
[RpcService]
public interface IGameServerApi
{
    [RpcMethod(GameRpc.Methods.CLIENT_HELLO, RpcDirection.ClientToServer)]
    void ClientHello(ClientHello message);

    [RpcMethod(GameRpc.Methods.CLIENT_HELLO, RpcDirection.ClientToServer)]
    void OnClientHello(ClientHello message, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.LEAVE_GAME, RpcDirection.ClientToServer)]
    void LeaveGame(LeaveGameRequest message);

    [RpcMethod(GameRpc.Methods.LEAVE_GAME, RpcDirection.ClientToServer)]
    void OnLeaveGame(LeaveGameRequest message, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.CLIENT_HEARTBEAT, RpcDirection.ClientToServer)]
    void ClientHeartbeat(ClientHeartbeatMessage message);

    [RpcMethod(GameRpc.Methods.CLIENT_HEARTBEAT, RpcDirection.ClientToServer)]
    void OnClientHeartbeat(ClientHeartbeatMessage message, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.CLIENT_SNAPSHOT, RpcDirection.ClientToServer)]
    void ClientSnapshot(WorldSnapshot snapshot);

    [RpcMethod(GameRpc.Methods.CLIENT_SNAPSHOT, RpcDirection.ClientToServer)]
    void OnClientSnapshot(WorldSnapshot snapshot, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.CHAT, RpcDirection.ClientToServer)]
    void Chat(ChatMessage message);

    [RpcMethod(GameRpc.Methods.CHAT, RpcDirection.ClientToServer)]
    void OnChat(ChatMessage message, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.LIST_LEVELS, RpcDirection.ClientToServer)]
    Task<RpcResult<ListLevelsResponse>> ListLevelsAsync(
        ListLevelsRequest request,
        CancellationToken cancellationToken = default);

    [RpcMethod(GameRpc.Methods.LIST_LEVELS, RpcDirection.ClientToServer)]
    ValueTask<RpcResult<ListLevelsResponse>> ListLevelsAsync(
        ListLevelsRequest request, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.CREATE_LEVEL, RpcDirection.ClientToServer)]
    Task<RpcResult<NewLevelResponse>> CreateLevelAsync(
        NewLevelRequest request,
        CancellationToken cancellationToken = default);

    [RpcMethod(GameRpc.Methods.CREATE_LEVEL, RpcDirection.ClientToServer)]
    ValueTask<RpcResult<NewLevelResponse>> CreateLevelAsync(
        NewLevelRequest request, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.DELETE_LEVEL, RpcDirection.ClientToServer)]
    Task<RpcResult<DeleteLevelResponse>> DeleteLevelAsync(
        DeleteLevelRequest request,
        CancellationToken cancellationToken = default);

    [RpcMethod(GameRpc.Methods.DELETE_LEVEL, RpcDirection.ClientToServer)]
    ValueTask<RpcResult<DeleteLevelResponse>> DeleteLevelAsync(
        DeleteLevelRequest request, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.SAVE_LEVEL, RpcDirection.ClientToServer)]
    Task<RpcResult<SaveLevelResponse>> SaveLevelAsync(
        SaveLevelRequest request,
        CancellationToken cancellationToken = default);

    [RpcMethod(GameRpc.Methods.SAVE_LEVEL, RpcDirection.ClientToServer)]
    ValueTask<RpcResult<SaveLevelResponse>> SaveLevelAsync(
        SaveLevelRequest request, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.JOIN, RpcDirection.ClientToServer)]
    Task<RpcResult<JoinAccept>> JoinAsync(
        JoinRequest request,
        CancellationToken cancellationToken = default);

    [RpcMethod(GameRpc.Methods.JOIN, RpcDirection.ClientToServer)]
    ValueTask<RpcResult<JoinAccept>> JoinAsync(
        JoinRequest request, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.STREAM_LEVEL, RpcDirection.ClientToServer, Streams = typeof(LevelStreamChunk))]
    Task<RpcResult<LevelStreamComplete>> StreamLevelAsync(
        StreamLevelRequest request,
        IChunkSink<LevelStreamChunk> sink,
        CancellationToken cancellationToken = default);

    [RpcMethod(GameRpc.Methods.STREAM_LEVEL, RpcDirection.ClientToServer, Streams = typeof(LevelStreamChunk))]
    ValueTask<RpcResult<LevelStreamComplete>> StreamLevelAsync(
        StreamLevelRequest request, RpcContext context, CancellationToken cancellationToken);
}
