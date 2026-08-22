using System;
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
    private readonly Dictionary<Type, IComponentSnapshotBuilder> _builders;
    private readonly List<EntitySnapshotMsg> _pendingSnapshots = [];
    private readonly List<Uuid> _removedUuids = [];
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
            _builders[builders[i].ComponentType] = builders[i];
        }
    }

    public void Tick(float delta, DataStore store)
    {
        _tickNumber++;

        _pendingSnapshots.Clear();
        _removedUuids.Clear();

        OnTickAction onTick = new() { Owner = this };
        store.Query<NetworkComponent, OnTickAction>(delta, ref onTick);

        OnRemovedAction onRemoved = new() { Owner = this };
        store.QueryRemoved<NetworkComponent, OnRemovedAction>(0f, ref onRemoved);

        if (_transport.IsLocal)
        {
            return;
        }

        if (_pendingSnapshots.Count == 0 && _removedUuids.Count == 0)
        {
            return;
        }

        uint lastProcessedInput = 0;
        for (var i = 0; i < _pendingSnapshots.Count; i++)
        {
            EntitySnapshotMsg entitySnapshot = _pendingSnapshots[i];
            if (store.TryGet(Uuid.FromValue(entitySnapshot.Uuid), out int entity)
                && store.TryGet(entity, out NetworkComponent net))
            {
                if (net.LastAckedInput > lastProcessedInput)
                {
                    lastProcessedInput = net.LastAckedInput;
                }
            }
        }

        var removedUuids = new ulong[_removedUuids.Count];
        for (var i = 0; i < _removedUuids.Count; i++)
        {
            removedUuids[i] = _removedUuids[i].ToValue();
        }

        var snapshot = new WorldSnapshotMsg
        {
            TickNumber = _tickNumber,
            LastProcessedInput = lastProcessedInput,
            Entities = _pendingSnapshots.ToArray(),
            RemovedUuids = removedUuids,
        };

        _transport.Send(snapshot);
    }

    private struct OnTickAction : IForEach<NetworkComponent>
    {
        public NetworkReplicationSystem Owner;

        public void Execute(float delta, DataStore store, int entity, in NetworkComponent cleanupAudioPlayer)
        {
            var entitySnapshot = new EntitySnapshotMsg
            {
                Uuid = store.GetUuid(entity).ToValue(),
            };

            bool anyDirty = false;
            foreach (KeyValuePair<Type, IComponentSnapshotBuilder> pair in Owner._builders)
            {
                if (!store.IsDirty(pair.Key, entity))
                {
                    continue;
                }

                pair.Value.BuildSnapshot(store, entity, ref entitySnapshot);
                entitySnapshot.ComponentMask |= (uint)(1 << NetworkRegistry.GetBit(pair.Key));
                store.ClearDirty(pair.Key, entity);
                anyDirty = true;
            }

            if (anyDirty)
            {
                Owner._pendingSnapshots.Add(entitySnapshot);
            }
        }
    }

    private struct OnRemovedAction : IForEach<NetworkComponent>
    {
        public NetworkReplicationSystem Owner;

        public void Execute(float delta, DataStore store, int entity, in NetworkComponent cleanupAudioPlayer)
        {
            Owner._removedUuids.Add(store.GetUuid(entity));
            Owner.ClearDirty(store, entity);
        }
    }

    private void ClearDirty(DataStore store, int entity)
    {
        foreach (Type componentType in _builders.Keys)
        {
            store.ClearDirty(componentType, entity);
        }
    }
}
