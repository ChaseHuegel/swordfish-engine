using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Swordfish.ECS;
using Swordfish.Library.Util;

namespace WaywardBeyond.Shared.Networking.Transport;

/// <summary>
/// Server-side aggregation of one <see cref="IServerConnection"/> per connected client. Each client is
/// addressed by an opaque <see cref="Uuid"/> (<c>clientId</c>) assigned on <see cref="Add"/>; inbound
/// messages are tagged with the connection they arrived on so the server can route responses (spawn,
/// per-client snapshots) back to the correct client and key sessions. The hub lives in shared code (not
/// server core) because <see cref="LocalConnection"/> and <see cref="TcpTransport"/> are shared transport
/// implementations that feed it, and every world side (server core, tests) consumes it.
/// </summary>
public sealed class ServerConnectionHub
{
    private readonly ConcurrentDictionary<Uuid, IServerConnection> _connections = new();
    private readonly ConcurrentQueue<Uuid> _disconnects = new();
    private long _nextClientId;

    public int Count => _connections.Count;

    /// <summary>Registers a client connection and assigns its opaque client id.</summary>
    public Uuid Add(in IServerConnection connection)
    {
        long raw = Interlocked.Increment(ref _nextClientId);
        Uuid clientId = Uuid.FromValue((ulong)raw);
        _connections[clientId] = connection;
        return clientId;
    }

    /// <summary>
    /// Removes a client connection and queues it for disconnect processing. The returned messages are
    /// consumed by <see cref="DrainDisconnects"/> so the server teardown step can free the session's
    /// entity (replicating its despawn to remaining clients).
    /// </summary>
    public bool Remove(Uuid clientId)
    {
        if (!_connections.TryRemove(clientId, out _))
        {
            return false;
        }

        _disconnects.Enqueue(clientId);
        return true;
    }

    public bool TryGet(Uuid clientId, out IServerConnection connection)
    {
        return _connections.TryGetValue(clientId, out connection!);
    }

    /// <summary>
    /// Polls every connected client for a message type, tagging each with the client it arrived on. The
    /// connection list is snapshotted so a client removed mid-poll is not enumerated.
    /// </summary>
    public IEnumerable<(Uuid clientId, T message)> Receive<T>()
    {
        foreach (KeyValuePair<Uuid, IServerConnection> entry in _connections)
        {
            Result<T> result;
            while ((result = entry.Value.Receive<T>()).Success)
            {
                yield return (entry.Key, result.Value);
            }
        }
    }

    public Result Send<T>(Uuid clientId, in T message)
    {
        if (!_connections.TryGetValue(clientId, out IServerConnection? connection))
        {
            return Result.FromFailure($"No connection for client {clientId}.");
        }

        return connection.Send(message);
    }

    /// <summary>
    /// Enumerates all connected clients. Used by the replication publish stage to compose a per-client
    /// snapshot.
    /// </summary>
    public IEnumerable<(Uuid clientId, IServerConnection connection)> Clients
    {
        get
        {
            foreach (KeyValuePair<Uuid, IServerConnection> entry in _connections)
            {
                yield return (entry.Key, entry.Value);
            }
        }
    }

    /// <summary>Drains client ids removed since the last call (consumer frees their server entities).</summary>
    public IEnumerable<Uuid> DrainDisconnects()
    {
        while (_disconnects.TryDequeue(out Uuid clientId))
        {
            yield return clientId;
        }
    }
}