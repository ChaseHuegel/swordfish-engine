using System.Collections.Concurrent;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Server.Core;

public sealed class SessionManager
{
    private readonly ConcurrentDictionary<uint, int> _connectionToEntity = new();
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

    public Result<int> GetEntity(uint networkID)
    {
        if (_connectionToEntity.TryGetValue(networkID, out int entity))
        {
            return Result<int>.FromSuccess(entity);
        }

        return Result<int>.FromFailure($"No entity mapped to network ID {networkID}.");
    }
}
