using System;
using System.Collections.Generic;
using Swordfish.ECS;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Client-side replication. Publishes dirty client-owned components (e.g. input) upstream to the
/// server, automatically driven by ECS dirty tracking. Discrete <see cref="InteractionEvent"/> edges are
/// drained from the player's outbound <see cref="InteractionStageBuffer"/> and emitted as one snapshot
/// per edge, so rapid clicks between sends survive. Uploads are paced to
/// <see cref="NetworkingSettings.SnapshotHz"/>, not the ECS tick rate.
/// </summary>
internal sealed class ClientReplicationSystem : IEntitySystem
{
    private readonly IClientConnection _transport;
    private readonly NetworkingSettings _settings;
    private float _sinceSend;
    private readonly List<ComponentSnapshot> _pending = [];

    //  Entities whose staged (interaction-edge or inventory-op) buffers rode this tick's snapshot; their
    //  buffers are cleared only after the send succeeds so a failed send leaves them staged for re-emission.
    private readonly HashSet<int> _stagedEntities = [];

    //  Cached once per system instance: the per-call registry enumeration would otherwise allocate a
    //  fresh list on every tick of the replication hot path.
    private readonly NetworkComponentInfo[] _clientOwnedComponents;

    public ClientReplicationSystem(in IClientConnection transport, in NetworkingSettings settings)
    {
        _transport = transport;
        _settings = settings;
        _clientOwnedComponents = [.. NetworkRegistry.GetComponents(NetworkDirection.ClientOwned)];
    }

    public void Tick(float delta, DataStore store)
    {
        //  Upload at SnapshotHz, decoupled from the ECS tick rate. Dirty flags persist across skipped
        //  ticks and staged interaction/inventory edges stay buffered, so nothing is lost.
        _sinceSend += delta;
        if (_sinceSend < 1f / Math.Max(1, _settings.SnapshotHz.Get()))
        {
            return;
        }

        _sinceSend = 0f;
        _pending.Clear();
        _stagedEntities.Clear();

        //  Player-scoped: only the local player carries client-owned components, so collection never
        //  visits world structures.
        CollectAction action = new() { Owner = this };
        store.Query<PlayerComponent, CollectAction>(delta, ref action);

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

        //  Edges and ops are consumed exactly once: the outbound buffers are cleared only after the
        //  containing snapshot is actually sent. A failed send leaves them staged, so the next
        //  successful tick re-emits them instead of silently dropping clicks or moves.
        if (!_transport.Send(snapshot).Success)
        {
            return;
        }

        foreach (int entity in _stagedEntities)
        {
            if (store.TryGet(entity, out PendingInteractionComponent edges))
            {
                edges.Outbound.Clear();
            }

            if (store.TryGet(entity, out PendingInventoryComponent ops))
            {
                ops.Outbound.Clear();
            }
        }
    }

    private struct CollectAction : IForEach<PlayerComponent>
    {
        public ClientReplicationSystem Owner;

        public void Execute(float delta, DataStore store, int entity, in PlayerComponent player)
        {
            //  Buffered interaction edges and inventory ops are drained as their own snapshots before the
            //  dirty-scan, so a player who staged edges or moves this frame has them emitted even though
            //  the single component slots are no longer the transmission unit.
            if (store.TryGet(entity, out PendingInteractionComponent pending))
            {
                DrainInteractions(store, entity, pending);
            }

            if (store.TryGet(entity, out PendingInventoryComponent pendingOps))
            {
                DrainInventoryOps(store, entity, pendingOps);
            }

            foreach (NetworkComponentInfo info in Owner._clientOwnedComponents)
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

            //  The edges ride this tick's snapshot; the buffer is cleared after the send succeeds (see
            //  ClientReplicationSystem.Tick), so a failed send leaves them staged for the next tick.
            Owner._stagedEntities.Add(entity);
        }

        private void DrainInventoryOps(DataStore store, int entity, in PendingInventoryComponent pending)
        {
            if (!NetworkRegistry.TryGetInfo<InventoryEvent>(out NetworkComponentInfo info)
                || info.Codec is not IPayloadCodec<InventoryEvent> codec)
            {
                return;
            }

            InventoryOpStageBuffer.InventoryOp[] ops = pending.Outbound.Snapshot();
            if (ops.Length == 0)
            {
                return;
            }

            ulong entityUuid = store.GetUuid(entity).ToValue();
            for (var i = 0; i < ops.Length; i++)
            {
                InventoryOpStageBuffer.InventoryOp op = ops[i];
                byte[] payload = codec.Serialize(new InventoryEvent
                {
                    Entity = entityUuid,
                    SequenceNumber = op.SequenceNumber,
                    SlotMove = op.SlotMove,
                });
                if (payload.Length > 0)
                {
                    Owner._pending.Add(new ComponentSnapshot(entityUuid, info.Uuid.ToValue(), payload));
                }
            }

            //  The ops ride this tick's snapshot; the buffer is cleared after the send succeeds.
            Owner._stagedEntities.Add(entity);
        }
    }
}
