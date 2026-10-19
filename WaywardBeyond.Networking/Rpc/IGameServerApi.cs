using System;
using System.Threading;
using System.Threading.Tasks;
using NsdRpc;
using WaywardBeyond.Data;

namespace WaywardBeyond.Networking.Rpc;

/// <summary>
/// Every client-to-server call, notification, and stream. Invocation shapes (no
/// <see cref="RpcContext"/>) generate <c>GameServerApiClient</c>. Handler shapes (with a context)
/// generate <c>GameServerApiHandler</c>, which the server subclasses.
/// </summary>
[RpcService]
public interface IGameServerApi
{
    [RpcMethod(GameRpc.Methods.ClientHello, RpcDirection.ClientToServer)]
    void ClientHello(ClientHello message);

    [RpcMethod(GameRpc.Methods.ClientHello, RpcDirection.ClientToServer)]
    void OnClientHello(ClientHello message, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.LeaveGame, RpcDirection.ClientToServer)]
    void LeaveGame(LeaveGameRequest message);

    [RpcMethod(GameRpc.Methods.LeaveGame, RpcDirection.ClientToServer)]
    void OnLeaveGame(LeaveGameRequest message, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.ClientHeartbeat, RpcDirection.ClientToServer)]
    void ClientHeartbeat(ClientHeartbeatMessage message);

    [RpcMethod(GameRpc.Methods.ClientHeartbeat, RpcDirection.ClientToServer)]
    void OnClientHeartbeat(ClientHeartbeatMessage message, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.ClientSnapshot, RpcDirection.ClientToServer)]
    void ClientSnapshot(WorldSnapshot snapshot);

    [RpcMethod(GameRpc.Methods.ClientSnapshot, RpcDirection.ClientToServer)]
    void OnClientSnapshot(WorldSnapshot snapshot, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.Chat, RpcDirection.ClientToServer)]
    void Chat(ChatMessage message);

    [RpcMethod(GameRpc.Methods.Chat, RpcDirection.ClientToServer)]
    void OnChat(ChatMessage message, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.ListLevels, RpcDirection.ClientToServer)]
    Task<RpcResult<ListLevelsResponse>> ListLevelsAsync(
        ListLevelsRequest request,
        CancellationToken cancellationToken = default);

    [RpcMethod(GameRpc.Methods.ListLevels, RpcDirection.ClientToServer)]
    ValueTask<RpcResult<ListLevelsResponse>> ListLevelsAsync(
        ListLevelsRequest request, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.CreateLevel, RpcDirection.ClientToServer)]
    Task<RpcResult<NewLevelResponse>> CreateLevelAsync(
        NewLevelRequest request,
        CancellationToken cancellationToken = default);

    [RpcMethod(GameRpc.Methods.CreateLevel, RpcDirection.ClientToServer)]
    ValueTask<RpcResult<NewLevelResponse>> CreateLevelAsync(
        NewLevelRequest request, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.DeleteLevel, RpcDirection.ClientToServer)]
    Task<RpcResult<DeleteLevelResponse>> DeleteLevelAsync(
        DeleteLevelRequest request,
        CancellationToken cancellationToken = default);

    [RpcMethod(GameRpc.Methods.DeleteLevel, RpcDirection.ClientToServer)]
    ValueTask<RpcResult<DeleteLevelResponse>> DeleteLevelAsync(
        DeleteLevelRequest request, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.SaveLevel, RpcDirection.ClientToServer)]
    Task<RpcResult<SaveLevelResponse>> SaveLevelAsync(
        SaveLevelRequest request,
        CancellationToken cancellationToken = default);

    [RpcMethod(GameRpc.Methods.SaveLevel, RpcDirection.ClientToServer)]
    ValueTask<RpcResult<SaveLevelResponse>> SaveLevelAsync(
        SaveLevelRequest request, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.Join, RpcDirection.ClientToServer)]
    Task<RpcResult<JoinAccept>> JoinAsync(
        JoinRequest request,
        CancellationToken cancellationToken = default);

    [RpcMethod(GameRpc.Methods.Join, RpcDirection.ClientToServer)]
    ValueTask<RpcResult<JoinAccept>> JoinAsync(
        JoinRequest request, RpcContext context, CancellationToken cancellationToken);

    [RpcMethod(GameRpc.Methods.StreamLevel, RpcDirection.ClientToServer, Streams = typeof(LevelStreamChunk))]
    Task<RpcResult<LevelStreamComplete>> StreamLevelAsync(
        StreamLevelRequest request,
        IChunkSink<LevelStreamChunk> sink,
        CancellationToken cancellationToken = default);

    [RpcMethod(GameRpc.Methods.StreamLevel, RpcDirection.ClientToServer, Streams = typeof(LevelStreamChunk))]
    ValueTask<RpcResult<LevelStreamComplete>> StreamLevelAsync(
        StreamLevelRequest request, RpcContext context, CancellationToken cancellationToken);
}
