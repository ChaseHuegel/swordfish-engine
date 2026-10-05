using System.Collections.Concurrent;
using Swordfish.ECS;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Sessions;

namespace WaywardBeyond.Server.Core;

/// <summary>
/// Server-side registry binding each connection to its session and player entity. A connection is keyed
/// by the opaque client id handed out by the <c>ServerConnectionHub</c>; <see cref="Session"/> is the
/// gameplay handle stored on <see cref="NetworkComponent.Session"/>. One client id maps to at most one
/// session and one player entity.
/// </summary>
public sealed class SessionManager
{
    private readonly ConcurrentDictionary<Uuid, int> _clientToEntity = new();
    private readonly ConcurrentDictionary<Uuid, Session> _clientToSession = new();
    private readonly ConcurrentDictionary<Session, int> _sessionToEntity = new();
    private readonly ConcurrentDictionary<int, Uuid> _entityToClient = new();

    /// <summary>Number of live sessions (joined players); a world with zero is idle and eligible for unload.</summary>
    public int Count => _clientToSession.Count;

    /// <summary>
    /// Binds a client connection to a fresh session and its spawned player entity, also stamping
    /// <see cref="NetworkComponent.Session"/> on the entity.
    /// </summary>
    public void Register(DataStore store, int entity, Uuid clientId, Session session)
    {
        store.QueryRef<NetworkComponent>(entity, 0f, (float _, DataStore s, int e, ref Ref<NetworkComponent> net) =>
        {
            net.Write.Session = session;
        });

        _clientToEntity[clientId] = entity;
        _clientToSession[clientId] = session;
        _sessionToEntity[session] = entity;
        _entityToClient[entity] = clientId;
    }

    public bool TryGetEntity(Uuid clientId, out int entity)
    {
        return _clientToEntity.TryGetValue(clientId, out entity);
    }

    public bool TryGetSession(Uuid clientId, out Session session)
    {
        return _clientToSession.TryGetValue(clientId, out session);
    }

    public bool TryGetEntity(Session session, out int entity)
    {
        return _sessionToEntity.TryGetValue(session, out entity);
    }

    /// <summary>Returns the client connection bound to a player entity, if any (reverse of <see cref="TryGetEntity(Uuid, out int)"/>).</summary>
    public bool TryGetClient(int entity, out Uuid clientId)
    {
        return _entityToClient.TryGetValue(entity, out clientId);
    }

    /// <summary>Removes every mapping bound to a client id (disconnect teardown).</summary>
    public void EndSession(Uuid clientId)
    {
        if (_clientToSession.TryRemove(clientId, out Session session))
        {
            _sessionToEntity.TryRemove(session, out _);
        }

        if (_clientToEntity.TryRemove(clientId, out int entity))
        {
            _entityToClient.TryRemove(entity, out _);
        }
    }
}