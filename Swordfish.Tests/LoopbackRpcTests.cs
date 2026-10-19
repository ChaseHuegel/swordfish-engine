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
