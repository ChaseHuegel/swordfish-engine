using System;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using WaywardBeyond.Server.Core;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Discovery;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Transport;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// A departed LAN peer must leave no residue: the hub drops it, the host's transport registry prunes
/// it, LanHost's id map prunes it, and the owner disposes the dead transport (socket closed) exactly
/// once. Repeating the cycle must not grow any registry.
/// </summary>
public class LanHostLifecycleTests
{
    private static readonly INetworkSerializer[] _serializers =
    [
        new NsdMessageSerializer<JoinRequest>(),
        new NsdMessageSerializer<JoinAccept>(),
        new NsdMessageSerializer<WorldStreamComplete>(),
        new NsdMessageSerializer<WorldSnapshot>(),
        new NsdMessageSerializer<LeaveGameRequest>(),
    ];

    [Fact]
    public void RepeatedPeerDropsLeaveNoRegistryGrowth()
    {
        var settings = new NetworkingSettings();
        settings.ServerPort.Set(0);
        settings.LanDiscovery.Set(false);

        var hub = new ServerConnectionHub();
        var lanHost = new LanHost(_serializers, hub, settings, new LanHostInfo(), NullLoggerFactory.Instance);

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

                //  The host accepts and registers the peer.
                var deadline = Environment.TickCount + 5000;
                while (hub.Count == 0 && Environment.TickCount < deadline)
                {
                    Thread.Sleep(10);
                }
                Assert.Equal(1, hub.Count);

                raw.Close();

                //  The host observes the drop and prunes every registry; the dead transport is disposed.
                //  Poll tightly: under full-suite parallel load a 10ms sleep can stretch badly.
                deadline = Environment.TickCount + 10_000;
                while (hub.Count != 0 && Environment.TickCount < deadline)
                {
                    Thread.Sleep(2);
                }

Assert.Equal(0, hub.Count);
            }

            Assert.Empty((System.Collections.Concurrent.ConcurrentDictionary<TcpTransport, TcpTransport>)hostRegistry.GetValue(host)!);
            Assert.Empty((System.Collections.Concurrent.ConcurrentDictionary<TcpTransport, Swordfish.ECS.Uuid>)lanHostMap.GetValue(lanHost)!);
        }
        finally
        {
            lanHost.Dispose();
        }
    }
}