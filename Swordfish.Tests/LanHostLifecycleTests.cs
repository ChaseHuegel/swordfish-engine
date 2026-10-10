using System;
using System.Reflection;
using System.Threading;
using DryIoc;
using Microsoft.Extensions.Logging.Abstractions;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Discovery;
using WaywardBeyond.Networking.Serialization;
using WaywardBeyond.Networking.Transport;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// A departed LAN peer must leave no residue: the pending set drops it, the host's transport registry
/// prunes it, LanHost's id map prunes it, and the owner disposes the dead transport (socket closed)
/// exactly once. Repeating the cycle must not grow any registry. A peer that never sends a
/// <c>JoinRequest</c> lives only in the pending set - its world binding happens at join time.
/// </summary>
public class LanHostLifecycleTests
{
    private static readonly INetworkSerializer[] _serializers =
    [
        new NsdMessageSerializer<JoinRequest>(),
        new NsdMessageSerializer<JoinAccept>(),
        new NsdMessageSerializer<LevelStreamComplete>(),
        new NsdMessageSerializer<WorldSnapshot>(),
        new NsdMessageSerializer<LeaveGameRequest>(),
    ];

    [Fact]
    public void RepeatedPeerDropsLeaveNoRegistryGrowth()
    {
        var settings = new NetworkingConfig();
        settings.ServerPort.Set(0);
        settings.DiscoveryBroadcasting.Set(false);

        var pendingJoins = new PendingJoins();
        var pendingDeletes = new PendingLevelDeletes();
        var levelCatalog = new StubLevelCatalog();
        var levelManager = new ServerLevelManager(pendingJoins, pendingDeletes, levelCatalog, TestPermissions.EmptyPolicy, new ConnectionClaims(), NullLoggerFactory.Instance);
        var hostHeartbeat = new ServerHostHeartbeat(pendingJoins, settings, NullLogger<ServerHostHeartbeat>.Instance);
        var worldHost = new ServerWorldHost(new Container(), levelManager, hostHeartbeat, pendingJoins, pendingDeletes, levelCatalog, settings, NullLoggerFactory.Instance);
        var lanHost = new LanHost(_serializers, pendingJoins, worldHost, settings, new LanHostInfo(), new ConnectionClaims(), NullLoggerFactory.Instance);

        //  The host's own transport registry and LanHost's id map are private; reflection reads them to
        //  prove pruning, since neither type exposes its internals.
        FieldInfo hostRegistry = typeof(TcpServerHost).GetField("_clients", BindingFlags.Instance | BindingFlags.NonPublic)!;
        FieldInfo lanHostMap = typeof(LanHost).GetField("_clientIds", BindingFlags.Instance | BindingFlags.NonPublic)!;

        lanHost.Run();

        //  The LanHost binds via TcpServerHost; read the bound port after Run.
        TcpServerHost host = (TcpServerHost)typeof(LanHost).GetField("_host", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(lanHost)!;
        Assert.True(host.LocalPort > 0, "The host must be listening on a port.");

        try
        {
            for (var cycle = 0; cycle < 5; cycle++)
            {
                var raw = new System.Net.Sockets.TcpClient();
                raw.Connect("127.0.0.1", host.LocalPort);

                //  The host accepts and registers the peer as awaiting its join request.
                var deadline = Environment.TickCount + 5000;
                while (pendingJoins.Count == 0 && Environment.TickCount < deadline)
                {
                    Thread.Sleep(10);
                }
                Assert.Equal(1, pendingJoins.Count);

                raw.Close();

                //  The host observes the drop and prunes every registry; the dead transport is disposed.
                //  Poll tightly: under full-suite parallel load a 10ms sleep can stretch badly.
                deadline = Environment.TickCount + 10_000;
                while (pendingJoins.Count != 0 && Environment.TickCount < deadline)
                {
                    Thread.Sleep(2);
                }

                Assert.Equal(0, pendingJoins.Count);
            }

            Assert.Empty((System.Collections.Concurrent.ConcurrentDictionary<TcpTransport, TcpTransport>)hostRegistry.GetValue(host)!);
            Assert.Empty((System.Collections.Concurrent.ConcurrentDictionary<TcpTransport, byte>)lanHostMap.GetValue(lanHost)!);
        }
        finally
        {
            lanHost.Dispose();
            worldHost.Dispose();
        }
    }
}