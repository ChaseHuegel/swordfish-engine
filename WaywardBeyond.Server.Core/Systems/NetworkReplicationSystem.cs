using System.Collections.Generic;
using DryIoc;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Snapshots;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core.Systems;

public sealed class NetworkReplicationSystem : IEntitySystem
{
    private readonly INetworkTransport _transport;
    private readonly ILogger<NetworkReplicationSystem> _logger;
    private readonly Dictionary<int, IComponentSnapshotBuilder> _builders;
    private readonly List<EntitySnapshotMsg> _pendingSnapshots = [];
    private uint _tickNumber;

    public NetworkReplicationSystem(
        in INetworkTransport transport,
        in ILogger<NetworkReplicationSystem> logger,
        IComponentSnapshotBuilder[] builders
    ) {
        _transport = transport;
        _logger = logger;

        _builders = [];
        for (var i = 0; i < builders.Length; i++)
        {
            int bit = NetworkRegistry.GetBit(builders[i].ComponentType);
            _builders[bit] = builders[i];
        }
    }

    public void Tick(float delta, DataStore store)
    {
        _tickNumber++;

        if (_transport.IsLocal)
        {
            ClearDirty(store);
            return;
        }

        _pendingSnapshots.Clear();

        store.Query<NetworkComponent, DirtyComponent>(delta, OnTick);

        if (_pendingSnapshots.Count == 0)
        {
            return;
        }

        uint lastProcessedInput = 0;
        for (var i = 0; i < _pendingSnapshots.Count; i++)
        {
            EntitySnapshotMsg entitySnapshot = _pendingSnapshots[i];
            if (store.Find((NetworkComponent net) => net.NetworkID == entitySnapshot.NetworkID, out int entity)
                && store.TryGet(entity, out NetworkComponent net))
            {
                if (net.LastAckedInput > lastProcessedInput)
                {
                    lastProcessedInput = net.LastAckedInput;
                }
            }
        }

        var snapshot = new WorldSnapshotMsg
        {
            TickNumber = _tickNumber,
            LastProcessedInput = lastProcessedInput,
            Entities = _pendingSnapshots.ToArray(),
        };

        _transport.Send(snapshot);
    }

    private void OnTick(float delta, DataStore store, int entity, ref NetworkComponent net, ref DirtyComponent dirty)
    {
        if (!dirty.Any())
        {
            return;
        }

        var entitySnapshot = new EntitySnapshotMsg
        {
            NetworkID = net.NetworkID,
        };

        dirty.ForEachDirty(bit =>
        {
            if (_builders.TryGetValue(bit, out IComponentSnapshotBuilder? builder))
            {
                builder.BuildSnapshot(store, entity, ref entitySnapshot);
                entitySnapshot.ComponentMask |= (uint)(1 << bit);
            }
        });

        _pendingSnapshots.Add(entitySnapshot);

        dirty.Clear();
        store.AddOrUpdate(entity, dirty);
    }

    private static void ClearDirty(DataStore store)
    {
        store.Query<NetworkComponent, DirtyComponent>(0f, (float d, DataStore s, int e, ref NetworkComponent net, ref DirtyComponent dirty) =>
        {
            dirty.Clear();
            s.AddOrUpdate(e, dirty);
        });
    }
}
