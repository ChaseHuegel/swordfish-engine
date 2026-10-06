using System;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Core;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Transport;
using Xunit;
using Xunit.Abstractions;

namespace Swordfish.Tests;

/// <summary>
/// Transport counters count every frame and byte on the actual send/receive paths with exact values,
/// and the session-heartbeat service emits at cadence and warns on lag.
/// </summary>
public class NetworkCountersAndHeartbeatTests
{
    private static readonly INetworkSerializer[] _serializers =
    [
        new NsdMessageSerializer<JoinRequest>(),
        new NsdMessageSerializer<JoinAccept>(),
        new NsdMessageSerializer<ClientHeartbeatMessage>(),
        new NsdMessageSerializer<ServerHeartbeatMessage>(),
    ];

    [Fact]
    public void LocalConnectionCountersAreExact()
    {
        var connection = new LocalConnection(_serializers);
        byte[] payload = new NsdMessageSerializer<JoinRequest>().Serialize(new JoinRequest
        {
            CharacterId = 7,
            PublicView = new PublicView { CharacterId = 7, Name = "C", Body = "wb:m_human" },
        });

        connection.Client.Send(new JoinRequest { CharacterId = 7, PublicView = new PublicView { CharacterId = 7, Name = "C", Body = "wb:m_human" } });
        connection.Client.Send(new JoinRequest { CharacterId = 7, PublicView = new PublicView { CharacterId = 7, Name = "C", Body = "wb:m_human" } });

        Assert.Equal(2, connection.Counters.PacketsSent);
        Assert.Equal(2 * payload.Length, connection.Counters.BytesSent);

        Assert.True(connection.Server.Receive<JoinRequest>().Success);
        Assert.True(connection.Server.Receive<JoinRequest>().Success);

        Assert.Equal(2, connection.Counters.PacketsReceived);
        Assert.Equal(2 * payload.Length, connection.Counters.BytesReceived);
    }

    [Fact]
    public void TcpTransportCountersAreExact()
    {
        using var server = new TcpTransport(_serializers, NullLoggerFactory.Instance);
        server.Listen(0);
        using var client = new TcpTransport(_serializers, NullLoggerFactory.Instance);
        client.Connect("127.0.0.1", server.LocalPort);

        client.Send(new JoinRequest { CharacterId = 3, PublicView = new PublicView { CharacterId = 3, Name = "T", Body = "wb:m_human" } });

        var deadline = Environment.TickCount + 5000;
        while (server.Receive<JoinRequest>().Success == false && Environment.TickCount < deadline)
        {
            Thread.Sleep(10);
        }

        Assert.Equal(1, client.PacketsSent);
        Assert.True(client.BytesSent > 0);

        Assert.Equal(1, server.PacketsReceived);
        Assert.True(server.BytesReceived > 0);

        //  The wire accounting includes the 4-byte length prefix on both sides, so they must agree.
        Assert.Equal(client.BytesSent, server.BytesReceived);
    }

    [Fact]
    public void ServerHeartbeatsEmitAtCadenceAndReportPlayers()
    {
        NetworkRegistry.Initialize([typeof(InputComponent).Assembly]);

        var settings = new NetworkingSettings();
        settings.ConnectionTimeoutMs.Set(4000);
        settings.HeartbeatIntervalMs.Set(250);

        var hub = new ServerConnectionHub();
        var connection = new LocalConnection(_serializers);
        hub.Add(connection.Server);

        var service = new ServerHeartbeatService(hub, settings, NullLogger<ServerHeartbeatService>.Instance);
        var store = new Swordfish.ECS.DataStore();

        //  A few pumps to measure TPS and pass the cadence window.
        var deadline = Environment.TickCount + 2000;
        while (Environment.TickCount < deadline)
        {
            service.Pump(1f / 64f, store, simTick: 100);
            Result<ServerHeartbeatMessage> heartbeat = connection.Client.Receive<ServerHeartbeatMessage>();
            if (heartbeat.Success)
            {
                Assert.True(heartbeat.Value.TPS >= 1);
                Assert.Equal(100u, heartbeat.Value.TickNumber);
                Assert.Equal(1u, heartbeat.Value.PlayerCount);
                return;
            }
            Thread.Sleep(20);
        }

        Assert.True(false, "No server heartbeat emitted at cadence.");
    }

    [Fact]
    public void HostHeartbeatKeepsPendingConnectionLive()
    {
        var settings = new NetworkingSettings();
        settings.ConnectionTimeoutMs.Set(4000);
        settings.HeartbeatIntervalMs.Set(250);

        var pendingJoins = new PendingJoins();
        var connection = new LocalConnection(_serializers);
        pendingJoins.Add(connection.Server);

        var service = new ServerHostHeartbeat(pendingJoins, settings, NullLogger<ServerHostHeartbeat>.Instance);

        //  A pending connection's client heartbeat is drained, not left queued.
        connection.Client.Send(new ClientHeartbeatMessage { TickNumber = 1, LastAppliedSnapshotTick = 1 });
        service.Tick(playerCount: 0);
        Assert.False(connection.Server.Receive<ClientHeartbeatMessage>().Success);

        //  The server heartbeat reaches the pending connection at cadence.
        var deadline = Environment.TickCount + 2000;
        while (Environment.TickCount < deadline)
        {
            service.Tick(playerCount: 0);
            Result<ServerHeartbeatMessage> heartbeat = connection.Client.Receive<ServerHeartbeatMessage>();
            if (heartbeat.Success)
            {
                Assert.True(heartbeat.Value.TPS >= 1);
                return;
            }
            Thread.Sleep(20);
        }

        Assert.True(false, "No server heartbeat emitted to a pending connection at cadence.");
    }

    [Fact]
    public void LaggingClientWarnsOncePerCrossing()
    {
        var settings = new NetworkingSettings();
        settings.ConnectionTimeoutMs.Set(4000);
        settings.HeartbeatIntervalMs.Set(250);
        settings.TickLagWarnThreshold.Set(10);

        var hub = new ServerConnectionHub();
        var connection = new LocalConnection(_serializers);
        Uuid clientId = hub.Add(connection.Server);

        var captured = new CapturingLogger<ServerHeartbeatService>();
        var service = new ServerHeartbeatService(hub, settings, captured);
        var store = new Swordfish.ECS.DataStore();

        //  The client reports a sim tick 50 behind the server.
        connection.Client.Send(new ClientHeartbeatMessage { TickNumber = 50, LastAppliedSnapshotTick = 50 });

        var deadline = Environment.TickCount + 2000;
        while (Environment.TickCount < deadline)
        {
            service.Pump(1f / 64f, store, simTick: 100);
            if (captured.Warnings.Count > 0)
            {
                break;
            }
            Thread.Sleep(20);
        }

        Assert.Contains(captured.Warnings, line => line.Contains($"{clientId}") && line.Contains("50"));
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public readonly System.Collections.Generic.List<string> Warnings = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                lock (Warnings)
                {
                    Warnings.Add(formatter(state, exception));
                }
            }
        }
    }
}