using System.Collections.Concurrent;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Sessions;

namespace WaywardBeyond.Server.Core;

public sealed class SessionManager
{
    private readonly ConcurrentDictionary<uint, int> _connectionToEntity = new();
    private readonly ConcurrentDictionary<Session, int> _sessionToEntity = new();
    private uint _nextNetworkID = 1;

    public uint AssignNetworkID(DataStore store, int entity)
    {
        uint networkID = _nextNetworkID++;

        store.AddOrUpdate(entity, new NetworkComponent
        {
            NetworkID = networkID,
        });

        _connectionToEntity[networkID] = entity;
        return networkID;
    }

    public void AssignSession(DataStore store, int entity, Session session)
    {
        store.Query<NetworkComponent>(entity, 0f, (float d, DataStore s, int e, ref NetworkComponent net) =>
        {
            net.Session = session;
            s.AddOrUpdate(e, net);
        });

        _sessionToEntity[session] = entity;
    }

    public Result<int> GetEntity(uint networkID)
    {
        if (_connectionToEntity.TryGetValue(networkID, out int entity))
        {
            return Result<int>.FromSuccess(entity);
        }

        return Result<int>.FromFailure($"No entity mapped to network ID {networkID}.");
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
