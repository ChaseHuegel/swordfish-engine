using System.Collections.Concurrent;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Sessions;

namespace WaywardBeyond.Server.Core;

public sealed class SessionManager
{
    private readonly ConcurrentDictionary<Uuid, int> _connectionToEntity = new();
    private readonly ConcurrentDictionary<Session, int> _sessionToEntity = new();

    public Uuid AssignUuid(DataStore store, int entity)
    {
        Uuid uuid = store.GetUuid(entity);

        store.AddOrUpdate(entity, new NetworkComponent());

        _connectionToEntity[uuid] = entity;
        return uuid;
    }

    public void AssignSession(DataStore store, int entity, Session session)
    {
        store.QueryRef<NetworkComponent>(entity, 0f, (float d, DataStore s, int e, ref Ref<NetworkComponent> net) =>
        {
            net.Write.Session = session;
        });

        _sessionToEntity[session] = entity;
    }

    public Result<int> GetEntity(Uuid uuid)
    {
        if (_connectionToEntity.TryGetValue(uuid, out int entity))
        {
            return Result<int>.FromSuccess(entity);
        }

        return Result<int>.FromFailure($"No entity mapped to uuid {uuid}.");
    }

    public Result<int> GetEntity(Session session)
    {
        if (_sessionToEntity.TryGetValue(session, out int entity))
        {
            return Result<int>.FromSuccess(entity);
        }

        return Result<int>.FromFailure($"No entity mapped to session {session}.");
    }

    public void RemoveSession(Session session)
    {
        _sessionToEntity.TryRemove(session, out _);
    }
}
