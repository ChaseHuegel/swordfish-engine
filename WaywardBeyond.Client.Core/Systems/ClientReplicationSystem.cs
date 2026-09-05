using System;
using System.Collections.Generic;
using Swordfish.ECS;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Client-side replication. Publishes dirty client-owned components (e.g. input) upstream to the
/// server, automatically driven by ECS dirty tracking.
/// </summary>
internal sealed class ClientReplicationSystem : IEntitySystem
{
    private readonly IClientConnection _transport;
    private readonly List<ComponentSnapshot> _pending = [];

    public ClientReplicationSystem(in IClientConnection transport)
    {
        _transport = transport;
    }

    public void Tick(float delta, DataStore store)
    {
        _pending.Clear();

        CollectAction action = new() { Owner = this };
        store.Query(0f, ref action);

        if (_pending.Count == 0)
        {
            return;
        }

        var snapshot = new WorldSnapshot
        {
            TickNumber = 0,
            LastProcessedInput = 0,
            Components = _pending.ToArray(),
            RemovedEntities = [],
        };

        _transport.Send(snapshot);
    }

    private struct CollectAction : IForEach
    {
        public ClientReplicationSystem Owner;

        public void Execute(float delta, DataStore store, int entity)
        {
            foreach (NetworkComponentInfo info in NetworkRegistry.GetComponents(NetworkDirection.ClientOwned))
            {
                if (!store.IsDirty(info.Type, entity))
                {
                    continue;
                }

                byte[] payload = info.Codec.Serialize(store, entity);
                Owner._pending.Add(new ComponentSnapshot(store.GetUuid(entity).ToValue(), info.Uuid.ToValue(), payload));
                store.ClearDirty(info.Type, entity);
            }
        }
    }
}