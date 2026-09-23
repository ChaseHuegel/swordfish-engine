using System;
using System.Collections.Generic;
using Swordfish.ECS;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Client-side replication. Publishes dirty client-owned components (e.g. input) upstream to the
/// server, automatically driven by ECS dirty tracking. Discrete <see cref="InteractionEvent"/> edges are
/// drained from the player's outbound <see cref="InteractionStageBuffer"/> and emitted as one snapshot
/// per edge, so rapid clicks between sends survive.
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
            //  Buffered interaction edges are drained as their own snapshots before the dirty-scan, so a
            //  player who staged edges this frame has them emitted even though the single edge component is
            //  no longer the transmission unit.
            if (store.TryGet(entity, out PendingInteractionComponent pending))
            {
                DrainInteractions(store, entity, pending);
            }

            foreach (NetworkComponentInfo info in NetworkRegistry.GetComponents(NetworkDirection.ClientOwned))
            {
                if (info.Type == typeof(InteractionEvent))
                {
                    continue;
                }

                if (!store.IsDirty(info.Type, entity))
                {
                    continue;
                }

                byte[] payload = info.Codec.Serialize(store, entity);
                store.ClearDirty(info.Type, entity);

                if (payload.Length == 0)
                {
                    continue;
                }

                Owner._pending.Add(new ComponentSnapshot(store.GetUuid(entity).ToValue(), info.Uuid.ToValue(), payload));
            }
        }

        private void DrainInteractions(DataStore store, int entity, in PendingInteractionComponent pending)
        {
            if (!NetworkRegistry.TryGetInfo<InteractionEvent>(out NetworkComponentInfo info)
                || info.Codec is not IPayloadCodec<InteractionEvent> codec)
            {
                return;
            }

            InteractionEvent[] events = pending.Outbound.Snapshot();
            if (events.Length == 0)
            {
                return;
            }

            ulong entityUuid = store.GetUuid(entity).ToValue();
            for (var i = 0; i < events.Length; i++)
            {
                byte[] payload = codec.Serialize(in events[i]);
                if (payload.Length > 0)
                {
                    Owner._pending.Add(new ComponentSnapshot(entityUuid, info.Uuid.ToValue(), payload));
                }
            }

            //  Emitted edges are consumed; the buffer can be reset. The transport is in-process (LocalConnection),
            //  so a staged edge is always delivered; TCP/relayed clients keep their own outbound framing.
            pending.Outbound.Clear();
        }
    }
}
