#nullable enable
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NsdRpc;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Identity;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Levels;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking.Discovery;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Permissions;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;
using Xunit;

namespace WaywardBeyond.Tests;

public class ServerDiscoveryTests
{
    [Fact]
    public async Task DiscoverOverLoopbackReturnsLiveServerMetadata()
    {
        var config = new NetworkingConfig();
        config.Server.Name.Set("Outpost Alpha");
        var hostInfo = new LanHostInfo { ListenPort = 7777, PlayerCount = 3 };
        var transport = new LoopbackRpcServerTransport();
        var peers = new ServerPeers();
        ServerHandlers handlers = CreateHandlers(peers, config, hostInfo);

        using var serverHost = new ServerRpcHost(transport, peers, handlers, config, NullLoggerFactory.Instance);
        serverHost.Run();

        var connection = new LoopbackRpcClientConnection(transport);
        await connection.ConnectAsync();
        await WaitUntil(() => peers.Count > 0);

        var registry = new RpcRegistry();
        registry.Freeze();
        var client = new RpcClient(connection, registry);
        var api = new GameServerApiClient(client);

        RpcResult<ServerDiscovery> first = await api.DiscoverAsync(new ServerDiscoveryRequest());
        Assert.True(first.IsSuccess, first.ErrorMessage);
        Assert.Equal("Outpost Alpha", first.Value.ServerName);
        Assert.Equal(7777, first.Value.TcpPort);
        Assert.Equal(LanDiscovery.PROTOCOL_VERSION, first.Value.ProtocolVersion);
        Assert.Equal(3, first.Value.PlayerCount);

        config.Server.Name.Set("Outpost Beta");
        hostInfo.PlayerCount = 7;

        RpcResult<ServerDiscovery> second = await api.DiscoverAsync(new ServerDiscoveryRequest());
        Assert.True(second.IsSuccess, second.ErrorMessage);
        Assert.Equal("Outpost Beta", second.Value.ServerName);
        Assert.Equal(7, second.Value.PlayerCount);

        connection.Close();
    }

    [Fact]
    public async Task ServerPingServiceProbesTcpServerAndReflectsLiveUpdates()
    {
        var config = new NetworkingConfig();
        config.Server.Name.Set("Frontier Relay");
        config.Discovery.PingTimeoutMs.Set(2000);
        var hostInfo = new LanHostInfo { PlayerCount = 2 };
        var transport = new LoopbackRpcServerTransport();
        var peers = new ServerPeers();
        ServerHandlers handlers = CreateHandlers(peers, config, hostInfo);

        using var serverHost = new ServerRpcHost(transport, peers, handlers, config, NullLoggerFactory.Instance);
        serverHost.Run();
        serverHost.StartLan(0);
        hostInfo.ListenPort = serverHost.LanPort;

        var pingService = new ServerPingService(config);
        ServerProbeResult first = await pingService.ProbeAsync("127.0.0.1", serverHost.LanPort);

        Assert.True(first.Reachable);
        Assert.Equal("Frontier Relay", first.ServerName);
        Assert.Equal(2, first.PlayerCount);
        Assert.True(first.PingMs >= 0);

        config.Server.Name.Set("Frontier Core");
        hostInfo.PlayerCount = 5;

        ServerProbeResult second = await pingService.ProbeAsync("127.0.0.1", serverHost.LanPort);

        Assert.True(second.Reachable);
        Assert.Equal("Frontier Core", second.ServerName);
        Assert.Equal(5, second.PlayerCount);
        Assert.True(second.PingMs >= 0);
    }

    [Fact]
    public async Task ServerPingServiceReturnsUnreachableForClosedPort()
    {
        int unusedPort = GetUnusedTcpPort();
        var config = new NetworkingConfig();
        config.Discovery.PingTimeoutMs.Set(500);
        var pingService = new ServerPingService(config);

        ServerProbeResult result = await pingService.ProbeAsync("127.0.0.1", unusedPort);

        Assert.False(result.Reachable);
    }

    [Fact]
    public async Task ClientHeartbeatServiceMeasuresPingForLocalAndRemoteConnections()
    {
        var config = new NetworkingConfig();
        config.Protocol.HeartbeatIntervalMs.Set(250);
        config.Discovery.PingTimeoutMs.Set(2000);
        var hostInfo = new LanHostInfo();
        var transport = new LoopbackRpcServerTransport();
        var peers = new ServerPeers();
        ServerHandlers handlers = CreateHandlers(peers, config, hostInfo);

        using var serverHost = new ServerRpcHost(transport, peers, handlers, config, NullLoggerFactory.Instance);
        serverHost.Run();
        serverHost.StartLan(0);
        hostInfo.ListenPort = serverHost.LanPort;

        var profile = new ProfileSettings();
        profile.UserId.Set("ping-test-user");
        var inbound = new ClientInbound();
        var serverStats = new ServerStats();
        using var clientHost = new ClientRpcHost(
            transport,
            inbound,
            new ClientHandlers(inbound, serverStats),
            config,
            new ProfileUserClaimProvider(profile),
            NullLoggerFactory.Instance);

        using var heartbeat = new ClientHeartbeatService(
            clientHost,
            new SnapshotAckTracker(),
            motionProcessor: null,
            config,
            serverStats);

        //  Singleplayer / local loopback connection measures ping.
        await WaitUntil(() => !serverStats.IsPingStale(5000));
        Assert.True(serverHost.LanPort > 0);
        Assert.True(clientHost.IsLocal);
        Assert.True(serverStats.PingMs >= 0d);

        //  Switching to a remote TCP connection also measures ping.
        Assert.True(clientHost.ConnectRemote("127.0.0.1", serverHost.LanPort).Success);
        serverStats.ClearPing();
        await WaitUntil(() => !serverStats.IsPingStale(5000));
        Assert.True(clientHost.IsRemote);
        Assert.True(serverStats.PingMs >= 0d);
    }

    [Theory]
    [InlineData(0.25, "0.2 ms")]
    [InlineData(4.6, "4.6 ms")]
    [InlineData(10.0, "10 ms")]
    [InlineData(42.4, "42 ms")]
    [InlineData(128.7, "129 ms")]
    public void NetworkStatsOverlayFormatsPing(double pingMs, string expected)
    {
        Assert.Equal(expected, NetworkStatsOverlay.FormatPing(pingMs));
    }

    private static ServerHandlers CreateHandlers(ServerPeers peers, NetworkingConfig config, LanHostInfo hostInfo)
    {
        var manager = new ServerLevelManager(
            new EmptyLevelCatalog(),
            new PermissionResolver(PermissionPolicy.Create([])),
            NullLoggerFactory.Instance);
        var commands = new ServerHostCommands(
            manager,
            BrickRegistry.FromNames([]),
            config,
            NullLoggerFactory.Instance.CreateLogger<ServerHostCommands>());
        return new ServerHandlers(
            peers,
            commands,
            config,
            NullLoggerFactory.Instance.CreateLogger<ServerHandlers>(),
            hostInfo);
    }

    private static int GetUnusedTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        while (!condition() && Environment.TickCount64 < deadline)
        {
            await Task.Delay(20);
        }

        Assert.True(condition(), "The condition did not become true within the budget.");
    }

    private sealed class EmptyLevelCatalog : ILevelCatalog
    {
        public bool Create(string name, string seed, GameMode gameMode, out string levelGuid)
        {
            levelGuid = string.Empty;
            return false;
        }

        public Level[] ListLevels() => [];

        public bool Delete(string levelGuid) => false;

        public bool Exists(string levelGuid) => false;

        public ILevelStorage? Open(string levelGuid) => null;
    }
}
