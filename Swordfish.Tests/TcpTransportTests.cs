using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Serialization;
using WaywardBeyond.Networking.Transport;
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
        new NsdMessageSerializer<LevelStreamComplete>(),
        new NsdMessageSerializer<LeaveGameRequest>(),
        new NsdMessageSerializer<ChatMessage>(),
        new NsdMessageSerializer<VoxelEditMessage>(),
        new NsdMessageSerializer<NotificationMessage>(),
        new NsdMessageSerializer<SkillStateUpdateMessage>(),
        new NsdMessageSerializer<LevelEntityAdd>(),
        new NsdMessageSerializer<ServerHeartbeatMessage>(),
        new NsdMessageSerializer<ClientHeartbeatMessage>(),
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
        client.Send(new LevelStreamComplete { Dummy = 5 });
        client.Send(new LeaveGameRequest { Dummy = 9 });

        //  Each type arrives on its own queue; nothing was reordered into another type's slot.
        Assert.False(server.Receive<JoinAccept>().Success, "JoinAccept poll must not consume a JoinRequest frame.");
        Assert.False(server.Receive<WorldSnapshot>().Success, "WorldSnapshot poll must not consume another kind's frame.");

        Result<JoinRequest> join = PollFor<JoinRequest>(server);
        Assert.True(join.Success);
        Assert.Equal(11ul, join.Value.CharacterId);

        Result<LevelStreamComplete> stream = PollFor<LevelStreamComplete>(server);
        Assert.True(stream.Success);
        Assert.Equal(5, stream.Value.Dummy);

        Result<LeaveGameRequest> leave = PollFor<LeaveGameRequest>(server);
        Assert.True(leave.Success);
        Assert.Equal(9, leave.Value.Dummy);

        //  Everything consumed; no leftover frames of any kind.
        Assert.False(server.Receive<JoinRequest>().Success);
        Assert.False(server.Receive<LevelStreamComplete>().Success);
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
        server.Send(new LevelStreamComplete { Dummy = 3 });

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

        Result<LevelStreamComplete> stream = PollFor<LevelStreamComplete>(client);
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
        DisconnectReason disconnectReason = default;
        var disconnectedGate = new ManualResetEventSlim();
        client.OnDisconnected += reason =>
        {
            Interlocked.Increment(ref disconnectCount);
            disconnectReason = reason;
            disconnectedGate.Set();
        };

        //  The server shuts down its side of the connection.
        serverSide.Close();

        Assert.True(disconnectedGate.Wait(5000), "The client should observe the server shutdown.");
        Assert.Equal(1, disconnectCount);
        Assert.Equal(DisconnectReason.PeerClosed, disconnectReason);

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
        DisconnectReason reason = default;
        server.OnDisconnected += r => { reason = r; disconnectedGate.Set(); };

        using var raw = new TcpClient();
        raw.Connect("127.0.0.1", server.LocalPort);
        NetworkStream stream = raw.GetStream();

        //  A claimed 0x7FFFFFFF byte frame: must be rejected off the prefix alone.
        stream.Write([0xFF, 0xFF, 0xFF, 0x7F]);
        stream.Flush();

        Assert.True(disconnectedGate.Wait(5000), "The server must drop a peer claiming an oversized frame.");
        Assert.Equal(DisconnectReason.ReadError, reason);
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
        server.OnDisconnected += _ => disconnectedGate.Set();

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
    /// A full per-tick queue must drop only snapshot frames: control/state messages ride the
    /// never-evicting reliable queue, so one message per class survives an overloaded snapshot stream.
    /// A raw peer that stops reading freezes the send thread, making the drop path deterministic.
    /// </summary>
    [Fact]
    public void ReliableMessagesSurviveAFullPerTickQueue()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        using var client = new TcpTransport(_serializers, NullLoggerFactory.Instance, sendQueueSize: 4, sendIntervalMs: 1000);
        client.Connect("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);

        //  The peer side stays unread for the whole burst, so the client's send thread eventually
        //  blocks on a full OS buffer instead of draining the queue.
        using TcpClient peer = listener.AcceptTcpClient();

        //  A snapshot with a fat payload so a few frames exceed the OS socket buffer and freeze the
        //  send thread mid-write (the raw peer never reads).
        var payload = new byte[256 * 1024];
        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)i;
        }
        var bigSnapshot = new WorldSnapshot
        {
            Components = [new ComponentSnapshot(1, 2, payload)],
            RemovedEntities = [],
        };

        //  Overfill the per-tick queue (capacity 4): with the send thread blocked, each new snapshot
        //  drops the oldest buffered one.
        for (var i = 0; i < 8; i++)
        {
            Assert.True(client.Send(bigSnapshot).Success);
        }
        for (var i = 0; i < 8; i++)
        {
            Assert.True(client.Send(bigSnapshot).Success);
        }

        //  One control message per class: none may be dropped by the overloaded queue.
        Assert.True(client.Send(new JoinRequest { CharacterId = 1, PublicView = new PublicView { CharacterId = 1, Name = "A", Body = "wb:m_human" } }).Success);
        Assert.True(client.Send(new JoinAccept { PlayerEntity = 2 }).Success);
        Assert.True(client.Send(new LevelStreamComplete { Dummy = 3 }).Success);
        Assert.True(client.Send(new LeaveGameRequest { Dummy = 4 }).Success);
        Assert.True(client.Send(new ChatMessage { CharacterId = 5, SenderName = "A", Value = "hello" }).Success);
        Assert.True(client.Send(new VoxelEditMessage { EntityUuid = 6, X = 1, Y = 2, Z = 3, Sequence = 7, BrickId = "brick" }).Success);
        Assert.True(client.Send(new NotificationMessage { Type = 1, Key = "k", Args = ["a"] }).Success);
        Assert.True(client.Send(new SkillStateUpdateMessage { SkillId = "mining", TotalXP = 9, Level = 2, XPIntoLevel = 3, GainedXP = 1 }).Success);

        //  Drain the link now: everything the send thread actually wrote must be parseable, and every
        //  control message of every class must be present exactly once.
        var frames = new List<(int len, string tag, byte[] payload)>();
        var drainTask = Task.Run(() =>
        {
            var stream = peer.GetStream();
            stream.ReadTimeout = 2000;
            var lengthBuffer = new byte[4];
            try
            {
                while (stream.Read(lengthBuffer, 0, 4) == 4)
                {
                    int len = BitConverter.ToInt32(lengthBuffer, 0);
                    var frame = new byte[len];
                    int read = 0;
                    while (read < len)
                    {
                        int n = stream.Read(frame, read, len - read);
                        if (n == 0)
                        {
                            return;
                        }
                        read += n;
                    }

                    int tagLen = BitConverter.ToInt32(frame, 0);
                    string tag = Encoding.UTF8.GetString(frame, 4, tagLen);
                    var body = new byte[len - 4 - tagLen];
                    Array.Copy(frame, 4 + tagLen, body, 0, body.Length);
                    lock (frames)
                    {
                        frames.Add((len, tag, body));
                    }
                }
            }
            catch (System.IO.IOException)
            {
                //  Read timeout: no more frames are coming on a connection that stays open.
            }
        });

        Assert.True(drainTask.Wait(10_000), "Draining the buffered frames must complete.");
        listener.Stop();

        var counts = new Dictionary<string, int>();
        HashSet<string> known = _serializers.Select(s => s.MessageType.FullName!).ToHashSet();
        foreach ((int len, string tag, byte[] _) in frames)
        {
            Assert.Contains(tag, known);
            counts[tag] = counts.GetValueOrDefault(tag) + 1;
        }

        Assert.Equal(1, counts.GetValueOrDefault(typeof(JoinRequest).FullName!));
        Assert.Equal(1, counts.GetValueOrDefault(typeof(JoinAccept).FullName!));
        Assert.Equal(1, counts.GetValueOrDefault(typeof(LevelStreamComplete).FullName!));
        Assert.Equal(1, counts.GetValueOrDefault(typeof(LeaveGameRequest).FullName!));
        Assert.Equal(1, counts.GetValueOrDefault(typeof(ChatMessage).FullName!));
        Assert.Equal(1, counts.GetValueOrDefault(typeof(VoxelEditMessage).FullName!));
        Assert.Equal(1, counts.GetValueOrDefault(typeof(NotificationMessage).FullName!));
        Assert.Equal(1, counts.GetValueOrDefault(typeof(SkillStateUpdateMessage).FullName!));

        //  Some snapshots were written before the freeze, the rest were dropped oldest-first; the
        //  control frames above prove none of the drops were reliable frames.
        Assert.True(counts.GetValueOrDefault(typeof(WorldSnapshot).FullName!) >= 1);
    }

    /// <summary>
    /// A world stream over a congested transport must deliver every <see cref="LevelEntityAdd"/> in
    /// order plus the final <see cref="LevelStreamComplete"/>, even while the per-tick queue floods
    /// snapshot frames into the same socket.
    /// </summary>
    [Fact]
    public void WorldStreamDeliversAllEntitiesInOrderUnderSnapshotFlood()
    {
        using var server = new TcpTransport(_serializers, NullLoggerFactory.Instance);
        server.Listen(0);
        using var client = new TcpTransport(_serializers, NullLoggerFactory.Instance);
        client.Connect("127.0.0.1", server.LocalPort);

        const int entityCount = 40;
        for (var i = 1; i <= entityCount; i++)
        {
            server.Send(new LevelEntityAdd
            {
                VoxelEntity = new VoxelEntityData { Uuid = (ulong)i, X = i, Y = 0, Z = 0, Chunks = [] },
            });
        }
        server.Send(new LevelStreamComplete { Dummy = 0 });

        //  Flood the per-tick queue while the stream is in flight.
        for (var i = 0; i < 200; i++)
        {
            server.Send(new WorldSnapshot { TickNumber = (uint)i, Components = [], RemovedEntities = [] });
        }

        for (var i = 1; i <= entityCount; i++)
        {
            Result<LevelEntityAdd> add = PollFor<LevelEntityAdd>(client);
            Assert.True(add.Success, $"World entity {i} must arrive.");
            Assert.Equal((ulong)i, add.Value.VoxelEntity.Uuid);
        }

        Result<LevelStreamComplete> complete = PollFor<LevelStreamComplete>(client);
        Assert.True(complete.Success, "The stream complete marker must arrive after the entities.");
    }

    /// <summary>
    /// A peer whose reliable backlog stays over the disconnect threshold for the disconnect window is
    /// marked broken, bounding the server's per-peer memory even though the reliable queue never evicts.
    /// </summary>
    [Fact]
    public void StalledPeerIsDroppedAfterReliableBacklogWindow()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        using var client = new TcpTransport(
            _serializers,
            NullLoggerFactory.Instance,
            sendIntervalMs: 60_000,
            reliableQueueConcernThreshold: 1,
            reliableQueueDisconnectThreshold: 2,
            reliableQueueDisconnectMs: 300
        );
        client.Connect("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
        using TcpClient peer = listener.AcceptTcpClient();

        var disconnectedGate = new ManualResetEventSlim();
        DisconnectReason reason = default;
        client.OnDisconnected += r => { reason = r; disconnectedGate.Set(); };

        //  A never-reading peer jams the send thread; three reliable frames overshoot the threshold.
        for (var i = 0; i < 3; i++)
        {
            Assert.True(client.Send(new JoinRequest { CharacterId = (ulong)i, PublicView = new PublicView { CharacterId = (ulong)i, Name = "P", Body = "wb:m_human" } }).Success);
        }

        //  The backlog must be held past the window before the peer is dropped.
        Assert.False(disconnectedGate.Wait(100), "A short backlog must not drop the peer yet.");
        Thread.Sleep(400);
        Assert.True(client.Send(new JoinRequest { CharacterId = 9, PublicView = new PublicView { CharacterId = 9, Name = "P", Body = "wb:m_human" } }).Success);

        Assert.True(disconnectedGate.Wait(5000), "The stalled peer must be dropped after the backlog window.");
        Assert.Equal(DisconnectReason.BacklogLimit, reason);
        listener.Stop();
    }

    /// <summary>
    /// A connect to an unreachable host must fail within <c>ConnectionTimeoutMs</c>: the synchronous
    /// socket connect otherwise waits out the OS retry schedule (tens of seconds), freezing the caller.
    /// 198.51.100.x is TEST-NET-2, a non-routable IANA documentation range.
    /// </summary>
    [Fact]
    public void ConnectToBlackholedHostFailsWithinTimeout()
    {
        using var transport = new TcpTransport(_serializers, NullLoggerFactory.Instance, connectionTimeoutMs: 400);

        var stopwatch = Stopwatch.StartNew();
        Assert.ThrowsAny<Exception>(() => transport.Connect("198.51.100.7", 7777));
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 5000, $"Connect must be bounded, took {stopwatch.ElapsedMilliseconds} ms.");
        Assert.False(transport.IsConnected, "The transport must be cleanly closed after a failed connect.");
    }

    /// <summary>
    /// Enabling trace logging must not alter message flow: frames still arrive intact and in order.
    /// </summary>
    [Fact]
    public void TraceLoggingLeavesMessageFlowUntouched()
    {
        using var server = new TcpTransport(_serializers, NullLoggerFactory.Instance, traceLogging: true);
        server.Listen(0);
        using var client = new TcpTransport(_serializers, NullLoggerFactory.Instance, traceLogging: true);
        client.Connect("127.0.0.1", server.LocalPort);

        client.Send(new JoinRequest { CharacterId = 3, PublicView = new PublicView { CharacterId = 3, Name = "T", Body = "wb:m_human" } });
        Result<JoinRequest> join = PollFor<JoinRequest>(server);
        Assert.True(join.Success);
        Assert.Equal(3ul, join.Value.CharacterId);

        server.Send(new WorldSnapshot { TickNumber = 11, Components = [], RemovedEntities = [] });
        Result<WorldSnapshot> snapshot = PollFor<WorldSnapshot>(client);
        Assert.True(snapshot.Success);
        Assert.Equal(11u, snapshot.Value.TickNumber);
    }

    /// <summary>
    /// The transport no longer emits its own keepalive: the app-level session heartbeat is the
    /// liveness signal now. App heartbeats sent within the timeout window keep a live-but-idle link
    /// from being misread as a dead peer; a truly idle link times out (the honest behavior).
    /// </summary>
    [Fact]
    public void HeartbeatsKeepIdlePeerConnected()
    {
        using var server = new TcpTransport(_serializers, NullLoggerFactory.Instance, connectionTimeoutMs: 600);
        server.Listen(0);
        using var client = new TcpTransport(_serializers, NullLoggerFactory.Instance, connectionTimeoutMs: 600);
        client.Connect("127.0.0.1", server.LocalPort);

        int clientDropped = 0;
        int serverDropped = 0;
        client.OnDisconnected += _ => Interlocked.Increment(ref clientDropped);
        server.OnDisconnected += _ => Interlocked.Increment(ref serverDropped);

        //  App heartbeats (client -> server, server -> client) keep both read directions fed within
        //  the 600ms timeout window, replacing the removed transport keepalive.
        var deadline = Environment.TickCount + 2200;
        while (Environment.TickCount < deadline)
        {
            client.Send(new ClientHeartbeatMessage { TickNumber = 1, LastAppliedSnapshotTick = 1 });
            server.Send(new ServerHeartbeatMessage { TPS = 64, TickNumber = 2, PlayerCount = 1 });
            Thread.Sleep(200);
        }

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

    /// <summary>
    /// A genuinely idle link (no app messages at all) now times out - there is no transport keepalive
    /// left to save it.
    /// </summary>
    [Fact]
    public void IdlePeerWithoutHeartbeatsTimesOut()
    {
        using var server = new TcpTransport(_serializers, NullLoggerFactory.Instance, connectionTimeoutMs: 600);
        server.Listen(0);
        using var client = new TcpTransport(_serializers, NullLoggerFactory.Instance, connectionTimeoutMs: 600);
        client.Connect("127.0.0.1", server.LocalPort);

        var disconnectedGate = new ManualResetEventSlim();
        client.OnDisconnected += _ => disconnectedGate.Set();

        Thread.Sleep(2000);

        Assert.True(disconnectedGate.Wait(2000), "An idle link with no heartbeats must time out.");
    }
}