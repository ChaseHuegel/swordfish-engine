using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Transport;
using WaywardBeyond.Client.Core.Networking;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Client-side reconciliation. Applies authoritative server-owned component snapshots (full state:
/// position, orientation, linear + angular velocity), trims <see cref="PendingInputComponent"/> up to
/// the server's last-processed sim tick, realigns the shared prediction step to the server's published
/// sim tick, and frees entities despawned by the server. Local prediction continues from the corrected
/// state, so concatenated snapshots never over-apply commands the server already collapsed.
/// </summary>
internal sealed class ClientReconcileSystem : IEntitySystem
{
    private readonly IClientConnection _transport;
    private readonly SnapshotAckTracker _snapshotAck;
    private readonly ClientPlayerMotionProcessor _motionProcessor;

    public ClientReconcileSystem(
        in IClientConnection transport,
        SnapshotAckTracker snapshotAck,
        in ClientPlayerMotionProcessor motionProcessor
    ) {
        _transport = transport;
        _snapshotAck = snapshotAck;
        _motionProcessor = motionProcessor;
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
            ApplyComponent(components[i], store);
        }

        ulong[] removed = snapshot.RemovedEntities;
        for (var i = 0; i < removed.Length; i++)
        {
            if (store.TryGet(Uuid.FromValue(removed[i]), out int entity))
            {
                store.Free(entity);
            }
        }

        TrimPendingInput(snapshot.LastProcessedInput, store);

        //  Align live prediction with the server's sim tick after the authoritative state is applied.
        _motionProcessor.Step?.AlignTo(snapshot.TickNumber);

        _snapshotAck.LastAppliedSnapshotTick = snapshot.TickNumber;
    }

    private void ApplyComponent(ComponentSnapshot snapshot, DataStore store)
    {
        Uuid entityUuid = Uuid.FromValue(snapshot.Entity);
        if (!store.TryGet(entityUuid, out int entity))
        {
            entity = store.Alloc(entityUuid);
        }

        if (!NetworkRegistry.TryGetInfo(Uuid.FromValue(snapshot.TypeUuid), out NetworkComponentInfo info)
            || info.Direction != NetworkDirection.ServerOwned)
        {
            return;
        }

        info.Codec.Apply(store, entity, snapshot.Payload);
    }

    private static void TrimPendingInput(uint ackTick, DataStore store)
    {
        TrimPendingAction action = new() { AckTick = ackTick };
        store.Query<PlayerComponent, PendingInputComponent, TrimPendingAction>(0f, ref action);
    }

    private struct TrimPendingAction : IForEach<PlayerComponent, PendingInputComponent>
    {
        public uint AckTick;

        public void Execute(float delta, DataStore store, int entity, in PlayerComponent player, in PendingInputComponent pending)
        {
            PendingInputComponent trimmed = pending;
            trimmed.AckUpTo(AckTick);
            store.AddOrUpdate(entity, trimmed);
        }
    }
}