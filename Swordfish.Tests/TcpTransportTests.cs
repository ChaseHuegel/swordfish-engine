using System;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Transport;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Phase 5.2: the socket transport frames each message with a length prefix + type tag and dispatches
/// to per-type queues, so a client/server exchanging several message kinds never steals another kind's
/// frame. This is the first real exercise of the peer path.
/// </summary>
public class TcpTransportTests
{
    private static readonly INetworkSerializer[] _serializers =
    [
        new NsdMessageSerializer<JoinRequest>(),
        new NsdMessageSerializer<JoinAccept>(),
        new NsdMessageSerializer<WorldSnapshot>(),
        new NsdMessageSerializer<WorldStreamComplete>(),
        new NsdMessageSerializer<LeaveGameRequest>(),
    ];

    private static TcpTransport CreateServer()
    {
        var server = new TcpTransport(_serializers, NullLoggerFactory.Instance);
        server.Listen(0);
        return server;
    }

    private static TcpTransport CreateClient(int port)
    {
        var client = new TcpTransport(_serializers, NullLoggerFactory.Instance);
        client.Connect("127.0.0.1", port);
        return client;
    }

    private static Result<T> PollFor<T>(TcpTransport transport, int timeoutMs = 5000)
    {
        var deadline = Environment.TickCount + timeoutMs;
        while (Environment.TickCount < deadline)
        {
            Result<T> result = transport.Receive<T>();
            if (result.Success)
            {
                return result;
            }
            Thread.Sleep(10);
        }
        return Result<T>.FromFailure("Timed out waiting for message.");
    }

    [Fact]
    public void ClientToServerFrameDoesNotStealAcrossTypes()
    {
        using TcpTransport server = CreateServer();
        using TcpTransport client = CreateClient(server.LocalPort);

        client.Send(new JoinRequest { CharacterId = 11, PublicView = new PublicView { CharacterId = 11, Name = "P", Body = 0 } });
        client.Send(new WorldStreamComplete { Dummy = 5 });
        client.Send(new LeaveGameRequest { Dummy = 9 });

        //  Each type arrives on its own queue; nothing was reordered into another type's slot.
        Assert.False(server.Receive<JoinAccept>().Success, "JoinAccept poll must not consume a JoinRequest frame.");
        Assert.False(server.Receive<WorldSnapshot>().Success, "WorldSnapshot poll must not consume another kind's frame.");

        Result<JoinRequest> join = PollFor<JoinRequest>(server);
        Assert.True(join.Success);
        Assert.Equal(11ul, join.Value.CharacterId);

        Result<WorldStreamComplete> stream = PollFor<WorldStreamComplete>(server);
        Assert.True(stream.Success);
        Assert.Equal(5, stream.Value.Dummy);

        Result<LeaveGameRequest> leave = PollFor<LeaveGameRequest>(server);
        Assert.True(leave.Success);
        Assert.Equal(9, leave.Value.Dummy);

        //  Everything consumed; no leftover frames of any kind.
        Assert.False(server.Receive<JoinRequest>().Success);
        Assert.False(server.Receive<WorldStreamComplete>().Success);
        Assert.False(server.Receive<LeaveGameRequest>().Success);
    }

    [Fact]
    public void ServerToClientFrameDoesNotStealAcrossTypes()
    {
        using TcpTransport server = CreateServer();
        using TcpTransport client = CreateClient(server.LocalPort);

        server.Send(new WorldSnapshot
        {
            TickNumber = 42,
            LastProcessedInput = 7,
            Components = [new ComponentSnapshot(0x111, 0x222, [1, 2, 3])],
            RemovedEntities = [0x333],
        });
        server.Send(new JoinAccept { PlayerEntity = 0xABCD });
        server.Send(new WorldStreamComplete { Dummy = 3 });

        Result<JoinRequest> stray = PollFor<JoinRequest>(client, timeoutMs: 300);
        Assert.False(stray.Success);

        Result<WorldSnapshot> snapshot = PollFor<WorldSnapshot>(client);
        Assert.True(snapshot.Success);
        Assert.Equal(42u, snapshot.Value.TickNumber);
        Assert.Equal(7u, snapshot.Value.LastProcessedInput);
        Assert.Equal(0x111ul, snapshot.Value.Components[0].Entity);
        Assert.Equal(new byte[] { 1, 2, 3 }, snapshot.Value.Components[0].Payload);
        Assert.Equal(0x333ul, snapshot.Value.RemovedEntities[0]);

        Result<JoinAccept> accept = PollFor<JoinAccept>(client);
        Assert.True(accept.Success);
        Assert.Equal(0xABCDul, accept.Value.PlayerEntity);

        Result<WorldStreamComplete> stream = PollFor<WorldStreamComplete>(client);
        Assert.True(stream.Success);
        Assert.Equal(3, stream.Value.Dummy);

        Assert.False(client.Receive<WorldSnapshot>().Success);
        Assert.False(client.Receive<JoinAccept>().Success);
    }
}