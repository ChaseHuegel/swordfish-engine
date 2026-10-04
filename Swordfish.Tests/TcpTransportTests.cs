using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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

        client.Send(new JoinRequest { CharacterId = 11, PublicView = new PublicView { CharacterId = 11, Name = "P", Body = "wb:m_human" } });
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

    /// <summary>
    /// A peer shutdown must surface through <see cref="TcpTransport.OnDisconnected"/> exactly once, and
    /// subsequent <see cref="TcpTransport.Send{T}"/> must never block the calling thread (it enqueues for
    /// the background send drainer). This pins the fix for the client hang on close after a lost server.
    /// </summary>
    [Fact]
    public void PeerShutdownRaisesDisconnectOnceAndSendDoesNotBlock()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        using var client = new TcpTransport(_serializers);
        client.Connect("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);

        using TcpClient serverSide = listener.AcceptTcpClient();
        int disconnectCount = 0;
        var disconnectedGate = new ManualResetEventSlim();
        client.OnDisconnected += () =>
        {
            Interlocked.Increment(ref disconnectCount);
            disconnectedGate.Set();
        };

        //  The server shuts down its side of the connection.
        serverSide.Close();

        Assert.True(disconnectedGate.Wait(5000), "The client should observe the server shutdown.");
        Assert.Equal(1, disconnectCount);

        var stopwatch = Stopwatch.StartNew();
        _ = client.Send(new LeaveGameRequest { Dummy = 1 });
        stopwatch.Stop();

        //  A send after the peer is gone must return promptly, never block the caller.
        Assert.True(stopwatch.ElapsedMilliseconds < 2000, "Send must not block after a peer disconnect.");
        Assert.Equal(1, disconnectCount);
        client.Disconnect();
        listener.Stop();
    }

    /// <summary>
    /// The receive loop must reject a length prefix beyond <c>maxFrameBytes</c> before allocating its
    /// buffer, dropping the peer as a protocol violation. A small cap keeps the test allocation-free.
    /// </summary>
    [Fact]
    public void OversizedFramePrefixDropsThePeerWithoutAllocation()
    {
        using var server = new TcpTransport(_serializers, NullLoggerFactory.Instance, maxFrameBytes: 1024);
        server.Listen(0);

        var disconnectedGate = new ManualResetEventSlim();
        server.OnDisconnected += disconnectedGate.Set;

        using var raw = new TcpClient();
        raw.Connect("127.0.0.1", server.LocalPort);
        NetworkStream stream = raw.GetStream();

        //  A claimed 0x7FFFFFFF byte frame: must be rejected off the prefix alone.
        stream.Write([0xFF, 0xFF, 0xFF, 0x7F]);
        stream.Flush();

        Assert.True(disconnectedGate.Wait(5000), "The server must drop a peer claiming an oversized frame.");
    }

    /// <summary>
    /// A frame whose body is shorter than its prefix claims must not wedge the receive loop: EOF finishes
    /// the partial read and raises <see cref="TcpTransport.OnDisconnected"/>.
    /// </summary>
    [Fact]
    public void TruncatedFrameSurfacesDisconnect()
    {
        using var server = new TcpTransport(_serializers, NullLoggerFactory.Instance, maxFrameBytes: 1024);
        server.Listen(0);

        var disconnectedGate = new ManualResetEventSlim();
        server.OnDisconnected += disconnectedGate.Set;

        using var raw = new TcpClient();
        raw.Connect("127.0.0.1", server.LocalPort);
        NetworkStream stream = raw.GetStream();

        //  Prefix claims 100 bytes; only 10 arrive before EOF.
        stream.Write([100, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
        stream.Flush();
        raw.Close();

        Assert.True(disconnectedGate.Wait(5000), "The server must surface a disconnect on a truncated frame.");
    }

    /// <summary>
    /// A live-but-idle peer must not be dropped by the socket read timeout: a keepalive heartbeat keeps
    /// each direction fed within the timeout window. This pins the tailscale disconnect, where an idle
    /// link read blocks a full <see cref="TcpTransport"/> timeout and is misread as a dead peer.
    /// </summary>
    [Fact]
    public void KeepaliveKeepsIdlePeerConnected()
    {
        using var server = new TcpTransport(_serializers, NullLoggerFactory.Instance, connectionTimeoutMs: 600, keepaliveIntervalMs: 250);
        server.Listen(0);
        using var client = new TcpTransport(_serializers, NullLoggerFactory.Instance, connectionTimeoutMs: 600, keepaliveIntervalMs: 250);
        client.Connect("127.0.0.1", server.LocalPort);

        int clientDropped = 0;
        int serverDropped = 0;
        client.OnDisconnected += () => Interlocked.Increment(ref clientDropped);
        server.OnDisconnected += () => Interlocked.Increment(ref serverDropped);

        //  Stay idle for well past the 600ms read timeout so a missing keepalive would falsely drop.
        Thread.Sleep(2200);

        Assert.Equal(0, clientDropped);
        Assert.Equal(0, serverDropped);

        //  The link is still fully functional for real traffic.
        client.Send(new JoinRequest { CharacterId = 5, PublicView = new PublicView { CharacterId = 5, Name = "K", Body = "wb:m_human" } });
        Result<JoinRequest> join = PollFor<JoinRequest>(server);
        Assert.True(join.Success);

        server.Send(new WorldSnapshot { TickNumber = 9, Components = [], RemovedEntities = [] });
        Result<WorldSnapshot> snapshot = PollFor<WorldSnapshot>(client);
        Assert.True(snapshot.Success);
        Assert.Equal(9u, snapshot.Value.TickNumber);
    }
}