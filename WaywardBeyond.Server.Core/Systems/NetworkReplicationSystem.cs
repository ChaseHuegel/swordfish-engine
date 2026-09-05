using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core.Systems;

/// <summary>
/// Server-side replication. Applies inbound client-owned snapshots (e.g. input) and publishes
/// authoritative server-owned component snapshots plus despawns for any entity that carries a
/// <see cref="NetworkComponent"/>, automatically driven by ECS dirty tracking.
/// </summary>
public sealed class NetworkReplicationSystem : IEntitySystem
{
    private readonly IServerConnection _transport;
    private readonly ILogger<NetworkReplicationSystem> _logger;

    private readonly List<ComponentSnapshot> _pending = [];
    private readonly List<ulong> _removed = [];
    private uint _tickNumber;

    public NetworkReplicationSystem(
        in IServerConnection transport,
        in ILogger<NetworkReplicationSystem> logger
    ) {
        _transport = transport;
        _logger = logger;
    }

    public void Tick(float delta, DataStore store)
    {
        ApplyInbound(store);

        _tickNumber++;
        _pending.Clear();
        _removed.Clear();

        OnTickAction onTick = new() { Owner = this };
        store.Query<NetworkComponent, OnTickAction>(delta, ref onTick);

        OnRemovedAction onRemoved = new() { Owner = this };
        store.QueryRemoved<NetworkComponent, OnRemovedAction>(0f, ref onRemoved);

        if (_pending.Count == 0 && _removed.Count == 0)
        {
            return;
        }

        uint lastProcessedInput = 0;
        for (var i = 0; i < _pending.Count; i++)
        {
            ComponentSnapshot snap = _pending[i];
            if (store.TryGet(Uuid.FromValue(snap.Entity), out int entity)
                && store.TryGet(entity, out NetworkComponent net)
                && net.LastAckedInput > lastProcessedInput)
            {
                lastProcessedInput = net.LastAckedInput;
            }
        }

        var snapshot = new WorldSnapshot
        {
            TickNumber = _tickNumber,
            LastProcessedInput = lastProcessedInput,
            Components = _pending.ToArray(),
            RemovedEntities = _removed.ToArray(),
        };

        _transport.Send(snapshot);
    }

    private void ApplyInbound(DataStore store)
    {
        Result<WorldSnapshot> receiveResult;
        while ((receiveResult = _transport.Receive<WorldSnapshot>()).Success)
        {
            WorldSnapshot snapshot = receiveResult.Value;
            ComponentSnapshot[] components = snapshot.Components;
            for (var i = 0; i < components.Length; i++)
            {
                ApplyComponent(store, components[i]);
            }
        }
    }

    private void ApplyComponent(DataStore store, ComponentSnapshot snapshot)
    {
        Uuid entityUuid = Uuid.FromValue(snapshot.Entity);
        if (!store.TryGet(entityUuid, out int entity))
        {
            _logger.LogWarning("Ignoring component snapshot for unknown entity {uuid}.", snapshot.Entity);
            return;
        }

        if (!NetworkRegistry.TryGetInfo(Uuid.FromValue(snapshot.TypeUuid), out NetworkComponentInfo info))
        {
            _logger.LogWarning("Ignoring component snapshot with unknown type uuid {uuid}.", snapshot.TypeUuid);
            return;
        }

        info.Codec.Apply(store, entity, snapshot.Payload);

        if (info.Type == typeof(InputComponent)
            && store.TryGet(entity, out InputComponent input))
        {
            store.QueryRef<NetworkComponent>(entity, 0f, (float _, DataStore s, int e, ref Ref<NetworkComponent> net) =>
            {
                if (input.SequenceNumber > net.Read.LastAckedInput)
                {
                    net.Write.LastAckedInput = input.SequenceNumber;
                }

                net.Write.LastAckedSnapshot = input.ServerTickAtSample;
            });
        }
    }

    private struct OnTickAction : IForEach<NetworkComponent>
    {
        public NetworkReplicationSystem Owner;

        public void Execute(float delta, DataStore store, int entity, in NetworkComponent net)
        {
            Uuid entityUuid = store.GetUuid(entity);

            foreach (NetworkComponentInfo info in NetworkRegistry.GetComponents(NetworkDirection.ServerOwned))
            {
                if (!store.IsDirty(info.Type, entity))
                {
                    continue;
                }

                byte[] payload = info.Codec.Serialize(store, entity);
                Owner._pending.Add(new ComponentSnapshot(entityUuid.ToValue(), info.Uuid.ToValue(), payload));
                store.ClearDirty(info.Type, entity);
            }
        }
    }

    private struct OnRemovedAction : IForEach<NetworkComponent>
    {
        public NetworkReplicationSystem Owner;

        public void Execute(float delta, DataStore store, int entity, in NetworkComponent net)
        {
            Owner._removed.Add(store.GetUuid(entity).ToValue());

            store.ClearDirty<NetworkComponent>(entity);
            foreach (NetworkComponentInfo info in NetworkRegistry.GetComponents(NetworkDirection.ServerOwned))
            {
                store.ClearDirty(info.Type, entity);
            }
        }
    }
}