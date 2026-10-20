#nullable enable
using System;
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
using WaywardBeyond.Server.Saves;
using Xunit;

namespace Swordfish.Tests;

public class ClientRpcTests
{
    [Fact]
    public async Task ClientHostUsesTheLoopbackAndRestoresItAfterARemoteDrop()
    {
        var catalog = new FakeLevelCatalog();
        var transport = new LoopbackRpcServerTransport();
        var config = new NetworkingConfig();
        var peers = new ServerPeers();
        var manager = new ServerLevelManager(catalog, PermissionPolicy.Create([]), NullLoggerFactory.Instance);
        var commands = new ServerHostCommands(
            peers,
            manager,
            BrickRegistry.FromNames([]),
            config,
            NullLoggerFactory.Instance.CreateLogger<ServerHostCommands>());
        var handlers = new ServerHandlers(
            peers,
            commands,
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
}
