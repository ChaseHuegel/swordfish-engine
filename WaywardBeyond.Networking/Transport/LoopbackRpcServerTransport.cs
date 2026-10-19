using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NsdRpc;
using RpcServerConnection = NsdRpc.IServerConnection;

namespace WaywardBeyond.Networking.Transport;

/// <summary>
/// The listener for in-process loopback RPC. Each <see cref="LoopbackRpcClientConnection.ConnectAsync"/>
/// creates a fresh paired server connection and queues it for <see cref="AcceptAsync"/>. The transport
/// stays open when a client closes, so the client host can restore the loopback after a remote
/// disconnect.
/// </summary>
public sealed class LoopbackRpcServerTransport : IRpcServerTransport
{
    private readonly object _gate = new();
    private readonly Queue<LoopbackRpcServerConnection> _pending = [];
    private readonly HashSet<LoopbackRpcServerConnection> _accepted = [];
    private readonly SemaphoreSlim _signal = new(0);
    private ulong _nextClientId;
    private volatile bool _closed;

    /// <summary>Pairs a new client endpoint with a server endpoint and queues it for accept.</summary>
    internal void Connect(LoopbackRpcEndpoint clientEndpoint)
    {
        var serverEndpoint = new LoopbackRpcEndpoint();
        clientEndpoint.Pair(serverEndpoint);
        serverEndpoint.Pair(clientEndpoint);

        ulong clientId = Interlocked.Increment(ref _nextClientId);
        var connection = new LoopbackRpcServerConnection(clientId, serverEndpoint, this);

        lock (_gate)
        {
            if (_closed)
            {
                serverEndpoint.Close(RpcDisconnectReason.LocalClose);
                throw new InvalidOperationException("The loopback server transport is closed.");
            }

            _accepted.Add(connection);
            _pending.Enqueue(connection);
        }

        _signal.Release();
    }

    public async Task<RpcServerConnection?> AcceptAsync()
    {
        while (!_closed)
        {
            await _signal.WaitAsync().ConfigureAwait(false);

            while (true)
            {
                LoopbackRpcServerConnection connection;
                lock (_gate)
                {
                    if (_closed)
                    {
                        return null;
                    }

                    if (_pending.Count == 0)
                    {
                        break;
                    }

                    connection = _pending.Dequeue();
                }

                //  A client that closed before the accept loop reached it is not handed to a peer.
                if (connection.IsConnected)
                {
                    return connection;
                }
            }
        }

        return null;
    }

    public void Close()
    {
        LoopbackRpcServerConnection[] connections;
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            connections = [.. _accepted];
            _accepted.Clear();
            _pending.Clear();
        }

        foreach (LoopbackRpcServerConnection connection in connections)
        {
            connection.Close(RpcDisconnectReason.LocalClose);
        }

        //  Wake a waiting AcceptAsync so it observes the close.
        _signal.Release();
    }

    internal void Remove(LoopbackRpcServerConnection connection)
    {
        lock (_gate)
        {
            _accepted.Remove(connection);
        }
    }
}
