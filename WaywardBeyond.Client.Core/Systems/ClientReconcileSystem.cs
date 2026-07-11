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
            int bit = NetworkRegistry.GetBit(builders[i].ComponentType);
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

            if (!store.Find((NetworkComponent net) => net.NetworkID == entitySnapshot.NetworkID, out int entity))
            {
                continue;
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

                store.Query(entity, 0f, (float d, DataStore s, int e, ref InputComponent existing, ref PhysicsComponent physics) =>
                {
                    existing = input;
                    s.AddOrUpdate(e, existing);
                });
            }

            store.AddOrUpdate(entity, pending);
        }

        _snapshotAck.LastAppliedSnapshotTick = snapshot.TickNumber;
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
