using System.Collections.Generic;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Snapshots;
using WaywardBeyond.Shared.Networking.Transport;
using WaywardBeyond.Client.Core.Networking;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class ClientReconcileSystem : IEntitySystem
{
    private readonly INetworkTransport _transport;
    private readonly Dictionary<int, IComponentSnapshotBuilder> _builders;
    private readonly SnapshotAckTracker _snapshotAck;

    public ClientReconcileSystem(
        in INetworkTransport transport,
        IComponentSnapshotBuilder[] builders,
        SnapshotAckTracker snapshotAck
    ) {
        _transport = transport;
        _snapshotAck = snapshotAck;
        _builders = [];

        for (var i = 0; i < builders.Length; i++)
        {
            int bit = NetworkRegistry.GetUuid(builders[i].ComponentType);
            _builders[bit] = builders[i];
        }
    }

    public void Tick(float delta, DataStore store)
    {
        if (_transport.IsLocal)
        {
            return;
        }

        Result<WorldSnapshotMsg> receiveResult;
        while ((receiveResult = _transport.Receive<WorldSnapshotMsg>()).Success)
        {
            ApplySnapshot(receiveResult.Value, store);
        }
    }

    private void ApplySnapshot(WorldSnapshotMsg snapshot, DataStore store)
    {
        for (var i = 0; i < snapshot.Entities.Length; i++)
        {
            EntitySnapshotMsg entitySnapshot = snapshot.Entities[i];

            if (!store.TryGet(Uuid.FromValue(entitySnapshot.Uuid), out int entity))
            {
                entity = store.Alloc(Uuid.FromValue(entitySnapshot.Uuid));
            }

            if (!store.TryGet(entity, out PendingInputComponent pending))
            {
                ApplyEntitySnapshot(entitySnapshot, store, entity);
                continue;
            }

            pending.AckUpTo(snapshot.LastProcessedInput);

            int pendingCount = pending.PendingCount;
            if (pendingCount == 0)
            {
                ApplyEntitySnapshot(entitySnapshot, store, entity);
                continue;
            }

            ApplyEntitySnapshot(entitySnapshot, store, entity);

            for (var j = 0; j < pendingCount; j++)
            {
                InputComponent input = pending.GetPending(j);

                ApplyPendingInputAction applyPending = new() { Input = input };
                store.QueryRef<InputComponent, PhysicsComponent, ApplyPendingInputAction>(entity, 0f, ref applyPending);
            }

            store.AddOrUpdate(entity, pending);
        }

        for (var i = 0; i < snapshot.RemovedUuids.Length; i++)
        {
            Uuid uuid = Uuid.FromValue(snapshot.RemovedUuids[i]);
            if (store.TryGet(uuid, out int entity))
            {
                store.Free(entity);
            }
        }

        _snapshotAck.LastAppliedSnapshotTick = snapshot.TickNumber;
    }

    private struct ApplyPendingInputAction : IForEachRef<InputComponent, PhysicsComponent>
    {
        public InputComponent Input;

        public void Execute(float delta, DataStore store, int entity, ref Ref<InputComponent> existing, ref Ref<PhysicsComponent> physics)
        {
            existing.Write = Input;
        }
    }

    private void ApplyEntitySnapshot(EntitySnapshotMsg entitySnapshot, DataStore store, int entity)
    {
        foreach (KeyValuePair<int, IComponentSnapshotBuilder> pair in _builders)
        {
            if ((entitySnapshot.ComponentMask & (1 << pair.Key)) != 0)
            {
                pair.Value.ApplySnapshot(entitySnapshot, store, entity);
            }
        }
    }
}
