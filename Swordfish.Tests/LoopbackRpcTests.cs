using System;
using System.Threading;
using System.Threading.Tasks;
using NsdRpc;
using WaywardBeyond.Data;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Networking.Transport;
using Xunit;
using RpcServerConnection = NsdRpc.IServerConnection;

namespace Swordfish.Tests;

public class LoopbackRpcTests
{
    [Fact]
    public async Task CallRoundTrips()
    {
        var transport = new LoopbackRpcServerTransport();
        var handler = new TestServerHandler();
        var server = CreateServer(transport, handler);
        server.Start();

        RpcClient client = await CreateClient(transport);
        var api = new GameServerApiClient(client);

        RpcResult<NewLevelResponse> result = await api
            .CreateLevelAsync(new NewLevelRequest { Name = "Test", Seed = "1", GameMode = 0 })
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(result.IsSuccess);
        Assert.Equal("level-1", result.Value.LevelGuid);
    }

    [Fact]
    public async Task NotificationSentBeforeTheServerPeerIsWiredIsNotLost()
    {
        var transport = new LoopbackRpcServerTransport();
        var clientConnection = new LoopbackRpcClientConnection(transport);
        await clientConnection.ConnectAsync();
        var clientRegistry = new RpcRegistry();
        clientRegistry.Freeze();
        var client = new RpcClient(clientConnection, clientRegistry);
        var api = new GameServerApiClient(client);

        //  The server has not accepted yet: the frame waits in the loopback endpoint.
        api.ClientHello(new ClientHello { UserId = "user-1" });

        var handler = new TestServerHandler();
        var server = CreateServer(transport, handler);
        server.Start();

        ClientHello hello = await handler.Hello.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("user-1", hello.UserId);
        clientConnection.Close();
    }

    [Fact]
    public async Task ServerTransportAcceptsANewClientAfterThePreviousCloses()
    {
        var transport = new LoopbackRpcServerTransport();
        var firstConnection = new LoopbackRpcClientConnection(transport);
        await firstConnection.ConnectAsync();
        RpcServerConnection first = Assert.IsType<LoopbackRpcServerConnection>(
            await transport.AcceptAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        ulong firstId = first.ClientId;

        firstConnection.Close();
        Assert.False(first.IsConnected);

        var secondConnection = new LoopbackRpcClientConnection(transport);
        await secondConnection.ConnectAsync();
        RpcServerConnection second = Assert.IsType<LoopbackRpcServerConnection>(
            await transport.AcceptAsync().WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.True(second.IsConnected);
        Assert.NotEqual(firstId, second.ClientId);
        secondConnection.Close();
    }

    [Fact]
    public async Task CallTimesOutWithRequestTimeout()
    {
        var transport = new LoopbackRpcServerTransport();
        var registry = new RpcRegistry();
        registry.Add(new SlowHandler());
        registry.Freeze();
        var server = new RpcServer(transport, registry);
        server.Start();

        var connection = new LoopbackRpcClientConnection(transport);
        await connection.ConnectAsync();
        var clientRegistry = new RpcRegistry();
        clientRegistry.Freeze();
        var client = new RpcClient(connection, clientRegistry);

        Task<RpcResult<NewLevelResponse>> call = client.InvokeAsync<NewLevelRequest, NewLevelResponse>(
            SlowHandler.Method,
            new NewLevelRequest { Name = "slow", Seed = "1", GameMode = 0 },
            TimeSpan.FromMilliseconds(50));

        long deadline = Environment.TickCount64 + 5000;
        while (!call.IsCompleted && Environment.TickCount64 < deadline)
        {
            client.Poll();
            await Task.Delay(5);
        }

        RpcResult<NewLevelResponse> result = await call.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.False(result.IsSuccess);
        Assert.Equal((int)RpcErrorCode.RequestTimeout, result.ErrorCode);
        //  Do not close here: nsd-rpc 0.3.0 leaves a canceled inbound entry whose token source the
        //  dispatcher already disposed. The disconnect path then throws. See issue 0052.
    }

    [Fact]
    public async Task CallCancelsWhenTheTokenFires()
    {
        var transport = new LoopbackRpcServerTransport();
        var registry = new RpcRegistry();
        registry.Add(new SlowHandler());
        registry.Freeze();
        var server = new RpcServer(transport, registry);
        server.Start();

        var connection = new LoopbackRpcClientConnection(transport);
        await connection.ConnectAsync();
        var clientRegistry = new RpcRegistry();
        clientRegistry.Freeze();
        var client = new RpcClient(connection, clientRegistry);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        RpcResult<NewLevelResponse> result = await client
            .InvokeAsync<NewLevelRequest, NewLevelResponse>(
                SlowHandler.Method,
                new NewLevelRequest { Name = "slow", Seed = "1", GameMode = 0 },
                cancellationToken: cancellation.Token)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(result.IsSuccess);
        Assert.Equal((int)RpcErrorCode.Cancelled, result.ErrorCode);
        //  See the timeout test and issue 0052: closing after a canceled call trips the upstream bug.
    }

    [Fact]
    public async Task CountersMatchTheOppositeSide()
    {
        var transport = new LoopbackRpcServerTransport();
        var firstConnection = new LoopbackRpcClientConnection(transport);
        await firstConnection.ConnectAsync();
        var serverConnection = (LoopbackRpcServerConnection)(await transport.AcceptAsync())!;

        byte[] frame = [4, 0, 0, 0, 1, 2, 3, 4];
        Assert.True(firstConnection.TrySend(frame));

        RpcConnectionStats sent = firstConnection.Stats;
        RpcConnectionStats received = serverConnection.Stats;
        Assert.Equal(1, sent.PacketsSent);
        Assert.Equal(frame.Length, sent.BytesSent);
        Assert.Equal(1, received.PacketsReceived);
        Assert.Equal(frame.Length, received.BytesReceived);

        firstConnection.Close();
    }

    private static RpcServer CreateServer(LoopbackRpcServerTransport transport, TestServerHandler handler)
    {
        var registry = new RpcRegistry();
        registry.AddService(handler);
        registry.Freeze();
        return new RpcServer(transport, registry);
    }

    private static async Task<RpcClient> CreateClient(LoopbackRpcServerTransport transport)
    {
        var connection = new LoopbackRpcClientConnection(transport);
        await connection.ConnectAsync();
        var registry = new RpcRegistry();
        registry.Freeze();
        return new RpcClient(connection, registry);
    }

    [RpcMethod(Method)]
    private sealed class SlowHandler : IRpcHandler<NewLevelRequest, NewLevelResponse>
    {
        public const ulong Method = 900;

        public async ValueTask<RpcResult<NewLevelResponse>> ExecuteAsync(
            NewLevelRequest request,
            RpcContext context,
            CancellationToken cancellationToken
        ) {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            return RpcResult<NewLevelResponse>.Success(new NewLevelResponse { LevelGuid = "late", Success = true });
        }
    }

    private sealed class TestServerHandler : GameServerApiHandler
    {
        public TaskCompletionSource<ClientHello> Hello { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void OnClientHello(
            ClientHello message,
            RpcContext context,
            CancellationToken cancellationToken
        ) {
            Hello.TrySetResult(message);
        }

        public override ValueTask<RpcResult<NewLevelResponse>> CreateLevelAsync(
            NewLevelRequest request,
            RpcContext context,
            CancellationToken cancellationToken
        ) {
            return ValueTask.FromResult(RpcResult<NewLevelResponse>.Success(new NewLevelResponse
            {
                LevelGuid = "level-1",
                Success = true,
            }));
        }
    }
}
