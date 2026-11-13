#nullable enable
using System;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NsdRpc;
using NsdRpc.Tcp;
using NsdRpc.Wire;
using Xunit;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Server;

namespace Swordfish.Tests;

/// <summary>
/// Pins the nsd-rpc transport policy the server relies on: a stalled peer is closed with
/// <see cref="RpcDisconnectReason.BacklogLimit"/>, and a silent peer is closed with
/// <see cref="RpcDisconnectReason.IdleTimeout"/>.
/// </summary>
public class TransportPolicyTests
{
    [Fact]
    public async Task StalledPeerIsClosedWithBacklogLimit()
    {
        var tcpOptions = new TcpOptions
        {
            Port = 0,
            IdleTimeout = TimeSpan.Zero,
            SendQueueBytes = 64 * 1024,
            SendBacklogDisconnectBytes = 32 * 1024,
            SendBacklogDisconnectMs = 25,
            SendBatchInterval = TimeSpan.Zero,
        };

        var listener = new TcpServerTransport(new RpcOptions(), tcpOptions);
        var recorder = new RecordingRpcServerTransport(listener);
        var server = new RpcServer(recorder, FrozenRegistry());
        var peers = new TaskCompletionSource<RpcPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reasons = new TaskCompletionSource<RpcDisconnectReason>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ClientConnected += peer => peers.TrySetResult(peer);
        server.ClientDisconnected += peer =>
        {
            if (recorder.TryTakeReason(peer.ClientId, out RpcDisconnectReason reason))
            {
                reasons.TrySetResult(reason);
            }
        };
        server.Start();

        using var client = await ConnectAndHandshakeAsync(listener.Port);
        RpcPeer serverPeer = await WaitWithTimeoutAsync(peers.Task, listener.Port, "ClientConnected");

        for (var index = 0; index < 5_000 && !reasons.Task.IsCompleted; index++)
        {
            var snapshot = new WorldSnapshot
            {
                TickNumber = (uint)index,
                Components = [new ComponentSnapshot(1, 2, new byte[8192])],
                RemovedEntities = [],
                RemovedComponents = [],
            };

            if (!serverPeer.Notify(GameRpc.Methods.SEND_WORLD_SNAPSHOT, in snapshot))
            {
                break;
            }

            //  NsdRpc.Tcp checks SendBacklogTracker.ExceedsWindow only on Enqueue.
            //  Pause once the queue passes the 32 KiB threshold so the 25 ms window
            //  elapses before the next enqueue, rather than filling 64 KiB in < 25 ms.
            if (serverPeer.Stats.QueuedBytes >= 32 * 1024)
            {
                await Task.Delay(35);
            }
        }

        RpcDisconnectReason disconnectReason = await WaitWithTimeoutAsync(reasons.Task, listener.Port, "BacklogLimit disconnect");
        Assert.Equal(RpcDisconnectReason.BacklogLimit, disconnectReason);
        server.Close();
        listener.Close();
    }

    [Fact]
    public async Task SilentPeerIsClosedWithIdleTimeout()
    {
        var tcpOptions = new TcpOptions
        {
            Port = 0,
            IdleTimeout = TimeSpan.FromMilliseconds(150),
            SendBacklogDisconnectBytes = 0,
            SendBacklogDisconnectMs = 0,
            SendBatchInterval = TimeSpan.Zero,
        };

        var listener = new TcpServerTransport(new RpcOptions(), tcpOptions);
        var recorder = new RecordingRpcServerTransport(listener);
        var server = new RpcServer(recorder, FrozenRegistry());
        var peers = new TaskCompletionSource<RpcPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reasons = new TaskCompletionSource<RpcDisconnectReason>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ClientConnected += peer => peers.TrySetResult(peer);
        server.ClientDisconnected += peer =>
        {
            if (recorder.TryTakeReason(peer.ClientId, out RpcDisconnectReason reason))
            {
                reasons.TrySetResult(reason);
            }
        };
        server.Start();

        using var client = await ConnectAndHandshakeAsync(listener.Port);
        _ = await WaitWithTimeoutAsync(peers.Task, listener.Port, "ClientConnected");

        RpcDisconnectReason disconnectReason = await WaitWithTimeoutAsync(reasons.Task, listener.Port, "IdleTimeout disconnect");
        Assert.Equal(RpcDisconnectReason.IdleTimeout, disconnectReason);
        server.Close();
        listener.Close();
    }

    private static async Task<T> WaitWithTimeoutAsync<T>(Task<T> task, int port, string operation, int timeoutSeconds = 5)
    {
        try
        {
            return await task.WaitAsync(TimeSpan.FromSeconds(timeoutSeconds));
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"{operation} on port {port} timed out after {timeoutSeconds} s.", ex);
        }
    }

    private static RpcRegistry FrozenRegistry()
    {
        var registry = new RpcRegistry();
        registry.Freeze();
        return registry;
    }

    private static async Task<TcpClient> ConnectAndHandshakeAsync(int port)
    {
        var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        client.NoDelay = true;

        NetworkStream stream = client.GetStream();
        byte[] payload = new Handshake(1).Serialize();
        byte[] frame = new byte[4 + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
        payload.CopyTo(frame, 4);
        await stream.WriteAsync(frame);

        byte[] lengthBuffer = new byte[4];
        await stream.ReadExactlyAsync(lengthBuffer);
        int length = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
        byte[] response = new byte[length];
        await stream.ReadExactlyAsync(response);

        return client;
    }
}
