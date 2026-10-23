using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using NsdRpc;

namespace WaywardBeyond.Networking.Transport;

/// <summary>The listener for in-process loopback RPC. It stays open when a client closes.</summary>
public sealed class LoopbackRpcServerTransport : IRpcServerTransport
{
    private readonly Channel<LoopbackRpcServerConnection> _pending = Channel.CreateUnbounded<LoopbackRpcServerConnection>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    private readonly HashSet<LoopbackRpcServerConnection> _accepted = [];
    private readonly Lock _gate = new();
    private ulong _nextClientId;
    private volatile bool _closed;

    /// <summary>Pairs a new client endpoint with a server endpoint and queues it for accept.</summary>
    internal void Connect(LoopbackRpcEndpoint clientEndpoint)
    {
        if (_closed)
        {
            throw new InvalidOperationException("The loopback server transport is closed.");
        }

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
        }

        if (!_pending.Writer.TryWrite(connection))
        {
            connection.Close(RpcDisconnectReason.LocalClose);
            throw new InvalidOperationException("The loopback server transport is closed.");
        }
    }

    /// <summary>Returns the next connected client, or null once the transport is closed.</summary>
    public async Task<IServerConnection?> AcceptAsync()
    {
        while (true)
        {
            LoopbackRpcServerConnection connection;
            try
            {
                connection = await _pending.Reader.ReadAsync().ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
                return null;
            }

            if (_closed)
            {
                connection.Close(RpcDisconnectReason.LocalClose);
                return null;
            }

            //  A client that closed before the accept loop reached it is not handed to a peer.
            if (connection.IsConnected)
            {
                return connection;
            }
        }
    }

    /// <summary>Closes the listener, every accepted client, and every client waiting to be accepted.</summary>
    public void Close()
    {
        LoopbackRpcServerConnection[] accepted;
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            accepted = [.. _accepted];
            _accepted.Clear();
        }

        _pending.Writer.TryComplete();

        foreach (LoopbackRpcServerConnection connection in accepted)
        {
            connection.Close(RpcDisconnectReason.LocalClose);
        }

        while (_pending.Reader.TryRead(out LoopbackRpcServerConnection queued))
        {
            queued.Close(RpcDisconnectReason.LocalClose);
        }
    }

    internal void Remove(LoopbackRpcServerConnection connection)
    {
        lock (_gate)
        {
            _accepted.Remove(connection);
        }
    }
}
