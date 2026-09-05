using System.Collections.Generic;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Transport;
using WaywardBeyond.Client.Core.Networking;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Client-side reconciliation. Applies authoritative server-owned component snapshots, trims and
/// replays locally pending input for prediction, and frees entities despawned by the server.
/// </summary>
internal sealed class ClientReconcileSystem : IEntitySystem
{
    private readonly IClientConnection _transport;
    private readonly SnapshotAckTracker _snapshotAck;

    public ClientReconcileSystem(
        in IClientConnection transport,
        SnapshotAckTracker snapshotAck
    ) {
        _transport = transport;
        _snapshotAck = snapshotAck;
    }

    public void Tick(float delta, DataStore store)
    {
        Result<WorldSnapshot> receiveResult;
        while ((receiveResult = _transport.Receive<WorldSnapshot>()).Success)
        {
            ApplySnapshot(receiveResult.Value, store);
        }
    }

    private void ApplySnapshot(WorldSnapshot snapshot, DataStore store)
    {
        ComponentSnapshot[] components = snapshot.Components;
        for (var i = 0; i < components.Length; i++)
        {
            ApplyComponent(components[i], snapshot.LastProcessedInput, store);
        }

        ulong[] removed = snapshot.RemovedEntities;
        for (var i = 0; i < removed.Length; i++)
        {
            if (store.TryGet(Uuid.FromValue(removed[i]), out int entity))
            {
                store.Free(entity);
            }
        }

        _snapshotAck.LastAppliedSnapshotTick = snapshot.TickNumber;
    }

    private void ApplyComponent(ComponentSnapshot snapshot, uint lastProcessedInput, DataStore store)
    {
        Uuid entityUuid = Uuid.FromValue(snapshot.Entity);
        if (!store.TryGet(entityUuid, out int entity))
        {
            entity = store.Alloc(entityUuid);
        }

        if (NetworkRegistry.TryGetInfo(Uuid.FromValue(snapshot.TypeUuid), out NetworkComponentInfo info)
            && info.Direction == NetworkDirection.ServerOwned)
        {
            info.Codec.Apply(store, entity, snapshot.Payload);
        }

        if (!store.TryGet(entity, out PendingInputComponent pending))
        {
            return;
        }

        pending.AckUpTo(lastProcessedInput);

        int pendingCount = pending.PendingCount;
        for (var j = 0; j < pendingCount; j++)
        {
            InputComponent input = pending.GetPending(j);

            ApplyPendingInputAction applyPending = new() { Input = input };
            store.QueryRef<InputComponent, PhysicsComponent, ApplyPendingInputAction>(entity, 0f, ref applyPending);
        }

        store.AddOrUpdate(entity, pending);
    }

    private struct ApplyPendingInputAction : IForEachRef<InputComponent, PhysicsComponent>
    {
        public InputComponent Input;

        public void Execute(float delta, DataStore store, int entity, ref Ref<InputComponent> existing, ref Ref<PhysicsComponent> physics)
        {
            existing.Write = Input;
        }
    }
}