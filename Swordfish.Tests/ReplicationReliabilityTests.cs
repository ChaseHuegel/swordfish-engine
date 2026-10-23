#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NsdRpc;
using NsdRpc.Tcp;
using Xunit;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking.Rpc;

namespace Swordfish.Tests;

/// <summary>
/// Proves that accepted snapshot notifications are never dropped. This is the congestion
/// counterpart to the stalled-peer backlog test: a reader that keeps draining receives every
/// removal, one-shot component, and ordering token.
/// </summary>
public class ReplicationReliabilityTests
{
    [Fact]
    public async Task EveryAcceptedSnapshotArrivesInOrder()
    {
        const int count = 500;
        var listener = new TcpServerTransport(new RpcOptions(), new TcpOptions
        {
            Port = 0,
            SendBatchInterval = TimeSpan.FromMilliseconds(1),
        });
        var server = new RpcServer(listener, FrozenRegistry());
        var connected = new TaskCompletionSource<RpcPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ClientConnected += peer => connected.TrySetResult(peer);
        server.Start();

        var connection = new TcpClientConnection(
            new RpcOptions(),
            new TcpOptions { Host = "127.0.0.1", Port = listener.Port });
        await connection.ConnectAsync();

        var inbound = new ClientInbound();
        var clientRegistry = new RpcRegistry();
        clientRegistry.AddService(new ClientHandlers(inbound));
        clientRegistry.Freeze();
        _ = new RpcClient(connection, clientRegistry);

        RpcPeer peer = await connected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        for (var tick = 1; tick <= count; tick++)
        {
            var snapshot = new WorldSnapshot
            {
                TickNumber = (uint)tick,
                LastProcessedInput = (uint)tick,
                Components = [new ComponentSnapshot((ulong)tick, 2, new byte[512])],
                RemovedEntities = [(ulong)tick],
                RemovedComponents = [new ComponentRemoval((ulong)tick, 2)],
            };

            Assert.True(peer.Notify(GameRpc.Methods.WORLD_SNAPSHOT, in snapshot));
        }

        long deadline = Environment.TickCount64 + 10_000;
        while (inbound.Snapshots.Count < count && Environment.TickCount64 < deadline)
        {
            await Task.Delay(10);
        }

        Assert.Equal(count, inbound.Snapshots.Count);

        RpcConnectionStats stats = peer.Stats;
        Assert.True(stats.PacketsSent >= count, $"Only {stats.PacketsSent} frames accepted.");
        Assert.True(stats.Writes < stats.PacketsSent, "Send batching did not coalesce frames into fewer writes.");

        var received = new List<WorldSnapshot>(count);
        while (inbound.Snapshots.TryDequeue(out ClientInbound.InboundSnapshot message))
        {
            Assert.False(message.IsBatch);
            received.Add(message.Full);
        }

        for (var index = 0; index < received.Count; index++)
        {
            uint expected = (uint)(index + 1);
            Assert.Equal(expected, received[index].TickNumber);
            Assert.Equal(expected, received[index].LastProcessedInput);
            Assert.Equal(expected, (uint)received[index].Components[0].Entity);
            Assert.Equal(512, received[index].Components[0].Payload.Length);
            Assert.Equal(expected, (uint)received[index].RemovedEntities[0]);
            Assert.Equal(expected, (uint)received[index].RemovedComponents[0].Entity);
        }

        connection.Close();
        server.Close();
        listener.Close();
    }

    private static RpcRegistry FrozenRegistry()
    {
        var registry = new RpcRegistry();
        registry.Freeze();
        return registry;
    }
}
