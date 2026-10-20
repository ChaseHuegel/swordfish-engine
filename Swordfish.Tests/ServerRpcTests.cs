#nullable enable
using System;
using System.Buffers.Binary;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NsdRpc;
using NsdRpc.Tcp;
using NsdRpc.Wire;
using WaywardBeyond.Bricks;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Levels;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Permissions;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Saves;
using Xunit;
using RpcServerConnection = NsdRpc.IServerConnection;

namespace Swordfish.Tests;

public class ServerRpcTests
{
    [Fact]
    public void PeersAreKeyedByHostAndClientIdWithoutCollision()
    {
        var peers = new ServerPeers();
        var hostA = new FakeHost();
        var hostB = new FakeHost();
        RpcPeer rpcA = CreateServerPeer(clientId: 1);
        RpcPeer rpcB = CreateServerPeer(clientId: 1);

        ServerPeer peerA = peers.Add(hostA, rpcA, isLocal: true);
        ServerPeer peerB = peers.Add(hostB, rpcB, isLocal: false);

        Assert.NotEqual(peerA.GameId, peerB.GameId);
        Assert.True(peers.TryGet(hostA, 1, out ServerPeer foundA));
        Assert.True(peers.TryGet(hostB, 1, out ServerPeer foundB));
        Assert.Same(peerA, foundA);
        Assert.Same(peerB, foundB);
        Assert.True(peers.TryGet(peerA.GameId, out ServerPeer byGameId));
        Assert.Same(peerA, byGameId);
    }

    [Fact]
    public void PeersRemoveIsIdempotent()
    {
        var peers = new ServerPeers();
        var host = new FakeHost();
        ServerPeer peer = peers.Add(host, CreateServerPeer(7), isLocal: false);

        Assert.Same(peer, peers.Remove(host, 7));
        Assert.Null(peers.Remove(host, 7));
        Assert.False(peers.TryGet(peer.GameId, out _));
    }

    [Fact]
    public void RecordingTransportReturnsTheOriginalConnectionAndRecordsTheReason()
    {
        var inner = new WaywardBeyond.Networking.Transport.LoopbackRpcServerTransport();
        var recorder = new RecordingRpcServerTransport(inner);
        var client = new WaywardBeyond.Networking.Transport.LoopbackRpcClientConnection(inner);
        client.ConnectAsync().GetAwaiter().GetResult();

        RpcServerConnection connection = Assert.IsType<WaywardBeyond.Networking.Transport.LoopbackRpcServerConnection>(
            recorder.AcceptAsync().GetAwaiter().GetResult());

        client.Close(RpcDisconnectReason.LocalClose);

        Assert.False(connection.IsConnected);
        Assert.True(recorder.TryTakeReason(connection.ClientId, out RpcDisconnectReason reason));
        Assert.Equal(RpcDisconnectReason.PeerClosed, reason);
    }

    [Fact]
    public void RemotePeerWithoutCreatePermissionIsDenied()
    {
        var catalog = new FakeLevelCatalog();
        var policy = PermissionPolicy.Create([]);
        var manager = new ServerLevelManager(catalog, policy, NullLoggerFactory.Instance);
        ServerPeer peer = CreateGamePeer(clientId: 4, isLocal: false);

        RpcResult<NewLevelResponse> result = manager.Create(peer, new NewLevelRequest
        {
            Name = "Denied",
            Seed = "1",
            GameMode = 0,
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(GameRpcErrors.PermissionDenied, result.ErrorCode);
        Assert.Equal(0, catalog.Created);
    }

    [Fact]
    public void LocalPeerMayCreate()
    {
        var catalog = new FakeLevelCatalog();
        var policy = PermissionPolicy.Create([]);
        var manager = new ServerLevelManager(catalog, policy, NullLoggerFactory.Instance);
        ServerPeer peer = CreateGamePeer(clientId: 5, isLocal: true);

        RpcResult<NewLevelResponse> result = manager.Create(peer, new NewLevelRequest
        {
            Name = "Allowed",
            Seed = "1",
            GameMode = 0,
        });

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Success);
        Assert.Equal(1, catalog.Created);
    }

    [Fact]
    public async Task LoopbackMenuCallsRoundTrip()
    {
        var catalog = new FakeLevelCatalog();
        var transport = new LoopbackRpcServerTransport();
        var commands = CreateHostCommands(catalog, out ServerPeers peers, out ServerHandlers handlers);
        using var host = new ServerRpcHost(transport, peers, handlers, new NetworkingConfig(), NullLoggerFactory.Instance);
        host.Run();

        var connection = new LoopbackRpcClientConnection(transport);
        await connection.ConnectAsync();
        var registry = new RpcRegistry();
        registry.Freeze();
        var client = new RpcClient(connection, registry);
        var api = new GameServerApiClient(client);

        Task<RpcResult<ListLevelsResponse>> list = api.ListLevelsAsync(new ListLevelsRequest());
        await PumpUntil(list, commands);

        RpcResult<ListLevelsResponse> listResult = await list;
        Assert.True(listResult.IsSuccess);
        Assert.True(listResult.Value.CanCreateSave);

        Task<RpcResult<NewLevelResponse>> create = api.CreateLevelAsync(new NewLevelRequest
        {
            Name = "Loopback",
            Seed = "1",
            GameMode = 0,
        });
        await PumpUntil(create, commands);

        RpcResult<NewLevelResponse> createResult = await create;
        Assert.True(createResult.IsSuccess);
        Assert.True(createResult.Value.Success);
        Assert.Equal(1, catalog.Created);

        connection.Close();
    }

    [Fact]
    public async Task TcpMenuCallsUseTheLanEndpointAndDenyRemoteCreate()
    {
        var catalog = new FakeLevelCatalog();
        var transport = new LoopbackRpcServerTransport();
        var commands = CreateHostCommands(catalog, out ServerPeers peers, out ServerHandlers handlers);
        using var host = new ServerRpcHost(transport, peers, handlers, new NetworkingConfig(), NullLoggerFactory.Instance);
        host.Run();
        host.StartLan(0);

        var tcpOptions = new TcpOptions { Host = "127.0.0.1", Port = host.LanPort };
        var connection = new TcpClientConnection(new RpcOptions(), tcpOptions);
        await connection.ConnectAsync();
        var registry = new RpcRegistry();
        registry.Freeze();
        var client = new RpcClient(connection, registry);
        var api = new GameServerApiClient(client);

        Task<RpcResult<ListLevelsResponse>> list = api.ListLevelsAsync(new ListLevelsRequest());
        await PumpUntil(list, commands);

        RpcResult<ListLevelsResponse> listResult = await list;
        Assert.True(listResult.IsSuccess);
        Assert.False(listResult.Value.CanCreateSave);

        Task<RpcResult<NewLevelResponse>> create = api.CreateLevelAsync(new NewLevelRequest
        {
            Name = "Remote",
            Seed = "1",
            GameMode = 0,
        });
        await PumpUntil(create, commands);

        RpcResult<NewLevelResponse> createResult = await create;
        Assert.False(createResult.IsSuccess);
        Assert.Equal(GameRpcErrors.PermissionDenied, createResult.ErrorCode);
        Assert.Equal(0, catalog.Created);

        connection.Close();
    }

    [Fact]
    public void RawEnvelopeRoundTrips()
    {
        var envelope = new Envelope(1, (byte)RpcMessageKind.Request, 1, GameRpc.Methods.ListLevels, new byte[] { 1, 2, 3 }, null);
        Envelope decoded = Envelope.Deserialize(envelope.Serialize());
        Assert.Equal(1, decoded.Version);
        Assert.Equal((byte)RpcMessageKind.Request, decoded.Kind);
        Assert.Equal(1UL, decoded.Id);
        Assert.Equal(GameRpc.Methods.ListLevels, decoded.Method);
        Assert.Equal([1, 2, 3], decoded.Payload);
    }

    [Fact]
    public async Task RawValidRequestGetsAResponse()
    {
        var catalog = new FakeLevelCatalog();
        var transport = new LoopbackRpcServerTransport();
        var commands = CreateHostCommands(catalog, out ServerPeers peers, out ServerHandlers handlers);
        using var host = new ServerRpcHost(transport, peers, handlers, new NetworkingConfig(), NullLoggerFactory.Instance);
        host.Run();

        var connection = new LoopbackRpcClientConnection(transport);
        var responses = new System.Collections.Concurrent.ConcurrentQueue<Envelope>();
        connection.FrameReceived += frame => responses.Enqueue(Envelope.Deserialize(frame.Slice(4)));
        await connection.ConnectAsync();

        long deadline = Environment.TickCount64 + 5000;
        while (peers.Count == 0 && Environment.TickCount64 < deadline)
        {
            await Task.Delay(10);
        }

        Send(connection, new Envelope(
            1,
            (byte)RpcMessageKind.Request,
            10,
            GameRpc.Methods.ListLevels,
            new ListLevelsRequest { Dummy = 0 }.Serialize(),
            null));

        while (responses.IsEmpty && Environment.TickCount64 < deadline)
        {
            commands.Tick(null!);
            await Task.Delay(10);
        }

        Envelope listed = await WaitForResponseAsync(responses, expectedCount: 1);
        Assert.Equal(10UL, listed.Id);
        Assert.Null(listed.Error);
        connection.Close();
    }

    [Fact]
    public async Task MalformedPayloadAnswersInvalidParamsAndKeepsTheConnectionOpen()
    {
        var catalog = new FakeLevelCatalog();
        var transport = new LoopbackRpcServerTransport();
        var commands = CreateHostCommands(catalog, out ServerPeers peers, out ServerHandlers handlers);
        using var host = new ServerRpcHost(transport, peers, handlers, new NetworkingConfig(), NullLoggerFactory.Instance);
        host.Run();

        var connection = new LoopbackRpcClientConnection(transport);
        var responses = new System.Collections.Concurrent.ConcurrentQueue<Envelope>();
        connection.FrameReceived += frame => responses.Enqueue(Envelope.Deserialize(frame.Slice(4)));
        await connection.ConnectAsync();

        long deadline = Environment.TickCount64 + 5000;
        while (peers.Count == 0 && Environment.TickCount64 < deadline)
        {
            await Task.Delay(10);
        }

        //  A string length of 65,535 with no bytes cannot decode.
        Send(connection, new Envelope(
            1,
            (byte)RpcMessageKind.Request,
            1,
            GameRpc.Methods.CreateLevel,
            [0x00, 0x00, 0xFF, 0xFF],
            null));

        while (responses.IsEmpty && Environment.TickCount64 < deadline)
        {
            commands.Tick(null!);
            await Task.Delay(10);
        }

        Envelope error = await WaitForResponseAsync(responses, expectedCount: 1);
        Assert.Equal((byte)RpcMessageKind.Response, error.Kind);
        Assert.Equal(1UL, error.Id);
        Assert.True(error.Error.HasValue);
        Assert.Equal((int)RpcErrorCode.InvalidParams, error.Error!.Value.Code);

        Send(connection, new Envelope(
            1,
            (byte)RpcMessageKind.Request,
            2,
            GameRpc.Methods.ListLevels,
            new ListLevelsRequest { Dummy = 0 }.Serialize(),
            null));

        while (responses.IsEmpty && Environment.TickCount64 < deadline)
        {
            commands.Tick(null!);
            await Task.Delay(10);
        }

        Envelope listed = await WaitForResponseAsync(responses, expectedCount: 1);
        Assert.Equal((byte)RpcMessageKind.Response, listed.Kind);
        Assert.Equal(2UL, listed.Id);
        Assert.Null(listed.Error);

        connection.Close();
    }

    private static void Send(LoopbackRpcClientConnection connection, Envelope envelope)
    {
        byte[] body = envelope.Serialize();
        byte[] frame = new byte[4 + body.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, body.Length);
        body.CopyTo(frame, 4);
        Assert.True(connection.TrySend(frame));
    }

    private static async Task<Envelope> WaitForResponseAsync(
        System.Collections.Concurrent.ConcurrentQueue<Envelope> responses,
        int expectedCount
    ) {
        long deadline = Environment.TickCount64 + 5000;
        while (responses.Count < expectedCount && Environment.TickCount64 < deadline)
        {
            await Task.Delay(10);
        }

        Envelope? last = null;
        while (responses.TryDequeue(out Envelope envelope))
        {
            last = envelope;
        }

        return last ?? throw new TimeoutException("No RPC response arrived.");
    }

    private static ServerHostCommands CreateHostCommands(
        FakeLevelCatalog catalog,
        out ServerPeers peers,
        out ServerHandlers handlers
    ) {
        var config = new NetworkingConfig();
        peers = new ServerPeers();
        var manager = new ServerLevelManager(catalog, PermissionPolicy.Create([]), NullLoggerFactory.Instance);
        var commands = new ServerHostCommands(manager,
            BrickRegistry.FromNames([]),
            config,
            NullLoggerFactory.Instance.CreateLogger<ServerHostCommands>());
        handlers = new ServerHandlers(
            peers,
            commands,
            NullLoggerFactory.Instance.CreateLogger<ServerHandlers>());
        return commands;
    }

    private static async Task PumpUntil(Task task, ServerHostCommands commands, int timeoutMs = 5000)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        while (!task.IsCompleted && Environment.TickCount64 < deadline)
        {
            //  In the host embedding the server thread calls this; the test drives it directly.
            commands.Tick(null!);
            await Task.Delay(10);
        }

        Assert.True(task.IsCompleted, "The RPC call did not complete within the budget.");
        await task;
    }

    private static ServerPeer CreateGamePeer(ulong clientId, bool isLocal)
    {
        var peers = new ServerPeers();
        var host = new FakeHost();
        return peers.Add(host, CreateServerPeer(clientId), isLocal);
    }

    private static RpcPeer CreateServerPeer(ulong clientId)
    {
        return new RpcPeer(new FakeServerConnection(clientId));
    }

    private sealed class FakeHost : IRpcHost
    {
        public PeerSet Peers => default;
    }

    private sealed class FakeServerConnection(ulong clientId) : RpcServerConnection, IRpcConnectionStats
    {
        public ulong ClientId { get; } = clientId;
        public EndPoint? RemoteEndPoint => null;
        public bool IsConnected => true;
        public RpcConnectionStats Stats => default;

        public void Close(RpcDisconnectReason reason = RpcDisconnectReason.LocalClose) { }

        public bool TrySend(ReadOnlySpan<byte> frame) => true;

        public event Action<RpcDisconnectReason>? Disconnected { add { } remove { } }

        public event RpcFrameHandler? FrameReceived { add { } remove { } }
    }

    private sealed class FakeLevelCatalog : ILevelCatalog
    {
        public int Created { get; private set; }

        public bool Create(string name, string seed, GameMode gameMode, out string levelGuid)
        {
            Created++;
            levelGuid = Guid.NewGuid().ToString();
            return true;
        }

        public Level[] ListLevels() => [];

        public bool Delete(string levelGuid) => true;

        public bool Exists(string levelGuid) => false;

        public ILevelStorage? Open(string levelGuid) => null;
    }
}
