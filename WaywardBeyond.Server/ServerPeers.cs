using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using NsdRpc;
using Swordfish.ECS;

namespace WaywardBeyond.Server;

/// <summary>
/// The live peer directory. Peers are keyed by a stable game <see cref="Uuid"/> and by the
/// <c>(IRpcHost, clientId)</c> pair, so two RPC server instances cannot collide when the library
/// assigns the same client id. The immutable snapshot keeps the world tick allocation-free.
/// </summary>
public sealed class ServerPeers
{
    private readonly object _gate = new();
    private readonly Dictionary<Uuid, ServerPeer> _byGameId = [];
    private readonly Dictionary<(IRpcHost Host, ulong ClientId), ServerPeer> _byRpc = [];
    private readonly Dictionary<IRpcHost, bool> _hosts = new();
    private readonly ConcurrentQueue<ServerPeer> _disconnected = new();
    private ServerPeer[] _snapshot = [];
    private long _nextGameId;

    /// <summary>The live peers. A read-only snapshot that does not change when peers join or leave.</summary>
    public ReadOnlySpan<ServerPeer> Peers => _snapshot;

    public int Count => _snapshot.Length;

    /// <summary>Records which RPC endpoint a host is, so a peer can be created before the connect event.</summary>
    public void RegisterHost(IRpcHost host, bool isLocal)
    {
        lock (_gate)
        {
            _hosts[host] = isLocal;
        }
    }

    public ServerPeer Add(IRpcHost host, RpcPeer rpc, bool isLocal)
    {
        return GetOrAdd(host, rpc, isLocal);
    }

    /// <summary>
    /// Returns the peer for one RPC client, creating it on first use. A handler can run before the
    /// server's connect event reaches the host, so the handler path is the first user in that race.
    /// </summary>
    public ServerPeer GetOrAdd(IRpcHost host, RpcPeer rpc, bool? isLocal = null)
    {
        if (host == null)
        {
            throw new ArgumentNullException(nameof(host));
        }

        if (rpc == null)
        {
            throw new ArgumentNullException(nameof(rpc));
        }

        lock (_gate)
        {
            if (_byRpc.TryGetValue((host, rpc.ClientId), out ServerPeer existing))
            {
                return existing;
            }

            bool local = isLocal ?? (_hosts.TryGetValue(host, out bool registered) && registered);
            ulong rawGameId = (ulong)Interlocked.Increment(ref _nextGameId);
            var peer = new ServerPeer(Uuid.FromValue(rawGameId), rpc, local);
            _byGameId.Add(peer.GameId, peer);
            _byRpc.Add((host, rpc.ClientId), peer);
            _snapshot = [.. _byGameId.Values];
            return peer;
        }
    }

    public bool TryGet(Uuid gameId, out ServerPeer peer)
    {
        lock (_gate)
        {
            return _byGameId.TryGetValue(gameId, out peer!);
        }
    }

    public bool TryGet(IRpcHost host, ulong clientId, out ServerPeer peer)
    {
        lock (_gate)
        {
            return _byRpc.TryGetValue((host, clientId), out peer!);
        }
    }

    public bool TryGet(RpcContext context, out ServerPeer peer)
    {
        return TryGet(context.Host, context.ClientId, out peer);
    }

    /// <summary>Removes a peer and queues it for the server thread. Returns null when already removed.</summary>
    public ServerPeer? Remove(IRpcHost host, ulong clientId)
    {
        ServerPeer? peer;
        lock (_gate)
        {
            if (!_byRpc.TryGetValue((host, clientId), out peer))
            {
                return null;
            }

            _byRpc.Remove((host, clientId));
            _byGameId.Remove(peer.GameId);
            _snapshot = [.. _byGameId.Values];
        }

        return peer;
    }

    /// <summary>Queues a removed peer for the server thread. Called once per departure.</summary>
    public void EnqueueDisconnected(ServerPeer peer)
    {
        _disconnected.Enqueue(peer);
    }

    public bool TryDequeueDisconnected(out ServerPeer peer)
    {
        return _disconnected.TryDequeue(out peer!);
    }
}
