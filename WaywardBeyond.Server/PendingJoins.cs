using System.Collections.Concurrent;
using System.Collections.Generic;
using WaywardBeyond.Networking.Transport;

namespace WaywardBeyond.Server;

/// <summary>
/// Connections accepted by the transports but not yet bound to a world hub: the LAN acceptor adds each
/// accepted peer here, and the in-process host seeds the loopback. The world host drains the set each
/// server tick, binds a connection to the hub of the world its <c>JoinRequest</c> names, and removes it
/// on disconnect. A connection is in this set or in exactly one world hub, never both.
/// </summary>
public sealed class PendingJoins
{
    private readonly ConcurrentDictionary<IServerConnection, byte> _connections = new();

    public int Count => _connections.Count;

    public void Add(in IServerConnection connection)
    {
        _connections.TryAdd(connection, 0);
    }

    public bool Remove(in IServerConnection connection)
    {
        return _connections.TryRemove(connection, out _);
    }

    public IEnumerable<IServerConnection> Snapshot()
    {
        return _connections.Keys;
    }
}