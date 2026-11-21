#nullable enable
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NsdRpc;
using NsdRpc.Tcp;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Identity;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Levels;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Permissions;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;
using Xunit;

namespace WaywardBeyond.Tests;

public class ClientRpcTests
{
    [Fact]
    public async Task ClientHostUsesTheLoopbackAndRestoresItAfterARemoteDrop()
    {
        var catalog = new FakeLevelCatalog();
        var transport = new LoopbackRpcServerTransport();
        var config = new NetworkingConfig();
        var peers = new ServerPeers();
        var manager = new ServerLevelManager(catalog, new PermissionResolver(PermissionPolicy.Create([])), NullLoggerFactory.Instance);
        var commands = new ServerHostCommands(manager,
            BrickRegistry.FromNames([]),
            config,
            NullLoggerFactory.Instance.CreateLogger<ServerHostCommands>());
        var handlers = new ServerHandlers(
            peers,
            commands,
            config,
            NullLoggerFactory.Instance.CreateLogger<ServerHandlers>());

        using var serverHost = new ServerRpcHost(transport, peers, handlers, config, NullLoggerFactory.Instance);
        serverHost.Run();

        var profile = new ProfileSettings();
        profile.UserId.Set("test-user");
        var claimProvider = new ProfileUserClaimProvider(profile);
        var inbound = new ClientInbound();
        using var clientHost = new ClientRpcHost(
            transport,
            inbound,
            new ClientHandlers(inbound),
            config,
            claimProvider,
            NullLoggerFactory.Instance);

        Assert.True(clientHost.IsLocal);
        Assert.NotNull(clientHost.Api);

        RpcResult<ListLevelsResponse> localList = await ListLevelsAsync(clientHost, commands);
        Assert.True(localList.IsSuccess);
        Assert.True(localList.Value.CanCreateSave);
        Assert.True(clientHost.Stats.PacketsSent > 0);
        Assert.True(clientHost.Stats.BytesSent > 0);

        serverHost.StartLan(0);
        Result connect = clientHost.ConnectRemote("127.0.0.1", serverHost.LanPort);
        Assert.True(connect.Success);
        Assert.True(clientHost.IsRemote);
        await WaitUntil(() => HasRemotePeer(peers));

        RpcResult<ListLevelsResponse> remoteList = await ListLevelsAsync(clientHost, commands);
        Assert.True(remoteList.IsSuccess);
        Assert.False(remoteList.Value.CanCreateSave);
        Assert.True(clientHost.Stats.PacketsSent > 0);
        Assert.True(clientHost.Stats.BytesSent > 0);

        var dropped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        clientHost.RemoteDisconnected += () => dropped.TrySetResult();
        clientHost.ActiveConnection!.Close(RpcDisconnectReason.PeerClosed);

        await dropped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitUntil(() => clientHost.IsLocal);

        RpcResult<ListLevelsResponse> restoredList = await ListLevelsAsync(clientHost, commands);
        Assert.True(restoredList.IsSuccess);
        Assert.True(restoredList.Value.CanCreateSave);

        //  A fresh remote switch closes the loopback again, and Disconnect restores it.
        Assert.True(clientHost.ConnectRemote("127.0.0.1", serverHost.LanPort).Success);
        Assert.True(clientHost.IsRemote);
        clientHost.Disconnect();
        await WaitUntil(() => clientHost.IsLocal);

        var gameClient = new GameClient(clientHost);
        Assert.True(gameClient.IsLocal);
        Assert.True(gameClient.IsConnected);
    }

    [Fact]
    public void StoppingServerBeforeClientEntersCleanTerminalStateWithoutLoggingErrors()
    {
        var logs = new RecordingLoggerFactory();
        var transport = new LoopbackRpcServerTransport();
        var config = new NetworkingConfig();
        var peers = new ServerPeers();
        var manager = new ServerLevelManager(new FakeLevelCatalog(), new PermissionResolver(PermissionPolicy.Create([])), NullLoggerFactory.Instance);
        var commands = new ServerHostCommands(manager,
            BrickRegistry.FromNames([]),
            config,
            NullLoggerFactory.Instance.CreateLogger<ServerHostCommands>());
        var handlers = new ServerHandlers(
            peers,
            commands,
            config,
            NullLoggerFactory.Instance.CreateLogger<ServerHandlers>());

        var serverHost = new ServerRpcHost(transport, peers, handlers, config, logs);
        serverHost.Run();

        var profile = new ProfileSettings();
        profile.UserId.Set("test-user");
        var inbound = new ClientInbound();
        using var clientHost = new ClientRpcHost(
            transport,
            inbound,
            new ClientHandlers(inbound),
            config,
            new ProfileUserClaimProvider(profile),
            logs);

        Assert.True(clientHost.IsLocal);

        bool remoteDisconnected = false;
        clientHost.RemoteDisconnected += () => remoteDisconnected = true;

        //  Application shutdown disposes the server host before the client host.
        serverHost.Dispose();

        Assert.True(transport.IsClosed);
        Assert.False(clientHost.IsConnected);
        Assert.False(clientHost.IsLocal);
        Assert.Null(clientHost.Api);
        Assert.False(remoteDisconnected);
        Assert.DoesNotContain(logs.Entries, entry => entry.Level >= LogLevel.Error);

        clientHost.Disconnect();
        Assert.False(clientHost.IsConnected);
        Assert.False(clientHost.ConnectRemote("127.0.0.1", 1).Success);
        Assert.DoesNotContain(logs.Entries, entry => entry.Level >= LogLevel.Error);
    }

    [Fact]
    public void CloseMarksClientClosedBeforeServerStops()
    {
        var logs = new RecordingLoggerFactory();
        var transport = new LoopbackRpcServerTransport();
        var config = new NetworkingConfig();
        var peers = new ServerPeers();
        var manager = new ServerLevelManager(new FakeLevelCatalog(), new PermissionResolver(PermissionPolicy.Create([])), NullLoggerFactory.Instance);
        var commands = new ServerHostCommands(manager,
            BrickRegistry.FromNames([]),
            config,
            NullLoggerFactory.Instance.CreateLogger<ServerHostCommands>());
        var handlers = new ServerHandlers(
            peers,
            commands,
            config,
            NullLoggerFactory.Instance.CreateLogger<ServerHandlers>());

        using var serverHost = new ServerRpcHost(transport, peers, handlers, config, logs);
        serverHost.Run();

        var profile = new ProfileSettings();
        profile.UserId.Set("test-user");
        var inbound = new ClientInbound();
        using var clientHost = new ClientRpcHost(
            transport,
            inbound,
            new ClientHandlers(inbound),
            config,
            new ProfileUserClaimProvider(profile),
            logs);

        bool remoteDisconnected = false;
        clientHost.RemoteDisconnected += () => remoteDisconnected = true;

        clientHost.Close();

        Assert.False(clientHost.IsConnected);
        Assert.Null(clientHost.Api);
        Assert.False(remoteDisconnected);
        Assert.False(clientHost.ConnectRemote("127.0.0.1", 1).Success);

        serverHost.Dispose();

        Assert.False(remoteDisconnected);
        Assert.DoesNotContain(logs.Entries, entry => entry.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task RemoteDropFollowedByDisconnectRestoresLoopbackAtMostOnce()
    {
        var logs = new RecordingLoggerFactory();
        var transport = new LoopbackRpcServerTransport();
        var config = new NetworkingConfig();
        var peers = new ServerPeers();
        var manager = new ServerLevelManager(new FakeLevelCatalog(), new PermissionResolver(PermissionPolicy.Create([])), NullLoggerFactory.Instance);
        var commands = new ServerHostCommands(manager,
            BrickRegistry.FromNames([]),
            config,
            NullLoggerFactory.Instance.CreateLogger<ServerHostCommands>());
        var handlers = new ServerHandlers(
            peers,
            commands,
            config,
            NullLoggerFactory.Instance.CreateLogger<ServerHandlers>());

        using var serverHost = new ServerRpcHost(transport, peers, handlers, config, logs);
        serverHost.Run();
        serverHost.StartLan(0);

        var profile = new ProfileSettings();
        profile.UserId.Set("test-user");
        var inbound = new ClientInbound();
        using var clientHost = new ClientRpcHost(
            transport,
            inbound,
            new ClientHandlers(inbound),
            config,
            new ProfileUserClaimProvider(profile),
            logs);

        Assert.True(clientHost.ConnectRemote("127.0.0.1", serverHost.LanPort).Success);
        await WaitUntil(() => HasRemotePeer(peers));

        var dropped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        clientHost.RemoteDisconnected += () =>
        {
            clientHost.Disconnect();
            dropped.TrySetResult();
        };

        clientHost.ActiveConnection!.Close(RpcDisconnectReason.PeerClosed);

        await dropped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitUntil(() => clientHost.IsLocal);
        clientHost.Disconnect();

        int restoreLogs = logs.Entries.Count(entry => entry.Message.Contains("Restored the loopback session.", StringComparison.Ordinal));
        Assert.Equal(1, restoreLogs);
        Assert.DoesNotContain(logs.Entries, entry => entry.Level >= LogLevel.Error);
    }

    [Fact]
    public void ClientHandlersDirectlyRecordsServerHeartbeatStats()
    {
        var inbound = new ClientInbound();
        var stats = new ServerStats();
        var handlers = new ClientHandlers(inbound, stats);

        var heartbeat = new WaywardBeyond.Data.Networking.ServerHeartbeatMessage
        {
            TPS = 30,
            PlayerCount = 4,
            TickNumber = 1234,
        };

        handlers.OnServerHeartbeat(heartbeat, default, default);

        Assert.Equal(30u, stats.TPS);
        Assert.Equal(4u, stats.PlayerCount);
        Assert.Equal(1234u, stats.ServerTick);
        Assert.False(stats.IsStale(2000));
    }

    [Fact]
    public void ClientHeartbeatIntervalClampsBelowIdleTimeout()
    {
        var config = new NetworkingConfig();
        config.Protocol.HeartbeatIntervalMs.Set(1000);
        config.Transport.IdleTimeoutMs.Set(5000);

        int interval = ClientHeartbeatService.GetServerHeartbeatInterval(config);
        Assert.Equal(1000, interval);

        config.Transport.IdleTimeoutMs.Set(1500);
        interval = ClientHeartbeatService.GetServerHeartbeatInterval(config);
        Assert.Equal(750, interval);

        config.Transport.IdleTimeoutMs.Set(300);
        interval = ClientHeartbeatService.GetServerHeartbeatInterval(config);
        Assert.Equal(250, interval);
    }

    private static async Task<RpcResult<ListLevelsResponse>> ListLevelsAsync(
        ClientRpcHost host,
        ServerHostCommands commands
    ) {
        Task<RpcResult<ListLevelsResponse>> list = host.Api!.ListLevelsAsync(new ListLevelsRequest());
        long deadline = Environment.TickCount64 + 5000;
        while (!list.IsCompleted && Environment.TickCount64 < deadline)
        {
            //  In the host embedding the server thread calls this; the test drives it directly.
            commands.Tick(null!);
            await Task.Delay(10);
        }

        Assert.True(list.IsCompleted, "The list call did not complete within the budget.");
        return await list;
    }

    private static bool HasRemotePeer(ServerPeers peers)
    {
        foreach (ServerPeer peer in peers.Peers)
        {
            if (!peer.IsLocal)
            {
                return true;
            }
        }

        return false;
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        while (!condition() && Environment.TickCount64 < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "The condition did not become true within the budget.");
    }

    private sealed class FakeLevelCatalog : ILevelCatalog
    {
        public bool Create(string name, string seed, GameMode gameMode, out string levelGuid)
        {
            levelGuid = Guid.NewGuid().ToString();
            return true;
        }

        public Level[] ListLevels() => [];

        public bool Delete(string levelGuid) => true;

        public bool Exists(string levelGuid) => false;

        public ILevelStorage? Open(string levelGuid) => null;
    }

    private sealed class RecordingLoggerFactory : ILoggerFactory
    {
        public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();

        public void AddProvider(ILoggerProvider provider) { }

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(Entries);

        public void Dispose() { }

        private sealed class RecordingLogger(ConcurrentQueue<(LogLevel Level, string Message)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter
            ) {
                entries.Enqueue((logLevel, formatter(state, exception)));
            }
        }
    }
}
