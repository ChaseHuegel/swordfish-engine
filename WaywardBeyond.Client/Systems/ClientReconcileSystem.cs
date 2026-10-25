using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Networking.Rpc;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// Client-side reconciliation. Drains authoritative snapshots, applies server-owned components, trims
/// <see cref="PendingInputComponent"/> to the server's ack, aligns the shared prediction step, and frees
/// despawned entities. Local-player motion is delegated to <see cref="LocalPlayerReconciler"/>, which
/// keeps the look client-predicted while reconciling collision rotation. The transition into
/// <c>Playing</c> coalesces any queued snapshot burst to the newest frame so a Loading-time backlog can
/// never hitch the first play tick.
/// </summary>
internal sealed class ClientReconcileSystem(
    ClientInbound inbound,
    SnapshotAckTracker snapshotAck,
    ClientPlayerMotionProcessor motionProcessor,
    LocalPlayerReconciler localPlayerReconciler,
    NetworkRegistry registry,
    ILogger<ClientReconcileSystem> logger
) : IEntitySystem
{
    private readonly FragmentAssembler _snapshotAssembler = new();
    private readonly List<WorldSnapshot> _snapshots = [];

    private bool _wasBelowPlaying = true;

    public void Tick(float delta, DataStore store)
    {
        //  Only reconcile once play has begun. During Loading the load/UI thread is still building the
        //  client's world entities (VoxelEntityBuilder.Create on a background thread), and applying
        //  world-body snapshots here would write Transform/Physics into those same entities concurrently
        //  with the build - a data race that corrupts the component slab. By the time Playing is set,
        //  GameSaveService.Load has finished building the world, so this thread is the only writer.
        //  The menu is skipped too: the client world is empty there and stale server snapshots would
        //  re-allocate stray entities.
        bool belowPlaying = WaywardBeyond.GameState < GameState.Playing;
        bool enteringPlay = _wasBelowPlaying && !belowPlaying;
        _wasBelowPlaying = belowPlaying;
        if (belowPlaying)
        {
            localPlayerReconciler.Reset();
            return;
        }

        DrainSnapshots();
        if (_snapshots.Count > 0)
        {
            if (enteringPlay)
            {
                //  Defensive: a burst queued during Loading is coalesced to the newest frame so the first
                //  play tick performs exactly one full ApplySnapshot. One-shot server state (e.g. the seeded
                //  inventory) rides only the early frames and must not be dropped, so each frame that is
                //  superseded has its non-motion components applied first; the newest applies fully and its
                //  transform/physics win (the per-frame motion history is otherwise useless).
                for (var index = 0; index < _snapshots.Count - 1; index++)
                {
                    ApplyOneShotComponents(_snapshots[index], store);
                }

                ApplySnapshot(_snapshots[^1], store);
            }
            else
            {
                foreach (WorldSnapshot snapshot in _snapshots)
                {
                    //  Re-check the gate inside the drain so an exit to the menu mid-drain stops applying
                    //  (and can no longer re-allocate entities the teardown is freeing).
                    if (WaywardBeyond.GameState < GameState.Playing)
                    {
                        localPlayerReconciler.Reset();
                        return;
                    }

                    ApplySnapshot(snapshot, store);
                }
            }
        }

        //  The orientation target advances every tick, not only on a snapshot, so the correction stays
        //  smooth between authoritative frames.
        localPlayerReconciler.Tick(delta, store);
    }

    /// <summary>
    /// Drains both snapshot queues and reassembles complete batches. Snapshots apply in tick order, so
    /// a full frame and a later batch never invert. An incomplete batch waits for its next part.
    /// </summary>
    private void DrainSnapshots()
    {
        _snapshots.Clear();

        while (inbound.Snapshots.TryDequeue(out ClientInbound.InboundSnapshot message))
        {
            try
            {
                if (!message.IsBatch)
                {
                    _snapshots.Add(message.Full);
                    continue;
                }

                SnapshotBatch batch = message.Batch;
                FragmentState state = _snapshotAssembler.Append(
                    batch.BatchId,
                    (int)batch.PartIndex,
                    (int)batch.PartCount,
                    batch.Payload,
                    out byte[] payload);

                if (state == FragmentState.Complete)
                {
                    _snapshots.Add(WorldSnapshot.Deserialize(payload));
                }
                else if (state == FragmentState.Rejected)
                {
                    logger.LogWarning("Dropping a snapshot batch with an invalid fragment.");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Dropping a malformed snapshot.");
            }
        }

        if (_snapshots.Count > 1)
        {
            _snapshots.Sort(static (left, right) => left.TickNumber.CompareTo(right.TickNumber));
        }
    }

    private void ApplySnapshot(WorldSnapshot snapshot, DataStore store)
    {
        ComponentSnapshot[] components = snapshot.Components;
        for (var i = 0; i < components.Length; i++)
        {
            ApplyComponent(components[i], store);
        }

        //  Component removals apply before despawns: same snapshot, so a removal can never be
        //  resurrected by a pre-removal delta. Bare entities persist; only RemovedEntities despawns.
        ComponentRemoval[] removedComponents = snapshot.RemovedComponents ?? [];
        for (var i = 0; i < removedComponents.Length; i++)
        {
            ComponentRemoval removal = removedComponents[i];
            Result<NetworkComponentInfo> lookup = registry.GetInfo(Uuid.FromValue(removal.TypeUuid));
            if (!lookup.Success || !store.TryGet(Uuid.FromValue(removal.Entity), out int entity))
            {
                continue;
            }

            store.Remove(lookup.Value.Type, entity);
        }

        ulong[] removed = snapshot.RemovedEntities ?? [];
        for (var i = 0; i < removed.Length; i++)
        {
            if (store.TryGet(Uuid.FromValue(removed[i]), out int entity))
            {
                store.Free(entity);
            }
        }

        //  The pending ring has not been trimmed yet, so the reconciler can read the look totals at the
        //  server's ack before the trim removes them.
        localPlayerReconciler.ApplySnapshot(store, snapshot.LastProcessedInput);

        TrimPendingInput(snapshot.LastProcessedInput, store);

        //  Align live prediction with the server's sim tick after the authoritative state is applied.
        motionProcessor.Step?.AlignTo(snapshot.TickNumber);

        snapshotAck.LastAppliedSnapshotTick = snapshot.TickNumber;
    }

    private void ApplyComponent(ComponentSnapshot snapshot, DataStore store)
    {
        Uuid entityUuid = Uuid.FromValue(snapshot.Entity);
        if (!store.TryGet(entityUuid, out int entity))
        {
            entity = store.Alloc(entityUuid);
        }

        Result<NetworkComponentInfo> lookup = registry.GetInfo(Uuid.FromValue(snapshot.TypeUuid));
        if (!lookup.Success || lookup.Value.Direction != NetworkDirection.ServerOwned)
        {
            return;
        }

        NetworkComponentInfo info = lookup.Value;

        if (!store.TryGet(entity, out PlayerComponent _))
        {
            ApplyCodec(store, entity, in info, snapshot.Payload);
            return;
        }

        //  The local player's look is client-predicted. The reconciler applies position/scale/linear
        //  velocity and uses the authoritative orientation and angular velocity to correct collision
        //  rotation without rewinding the local look.
        try
        {
            if (info.Type == typeof(TransformComponent))
            {
                TransformMessage message = TransformMessage.Deserialize(snapshot.Payload);
                localPlayerReconciler.ApplyTransform(
                    store,
                    entity,
                    new Vector3(message.PositionX, message.PositionY, message.PositionZ),
                    new Quaternion(message.OrientationX, message.OrientationY, message.OrientationZ, message.OrientationW),
                    new Vector3(message.ScaleX, message.ScaleY, message.ScaleZ)
                );
            }
            else if (info.Type == typeof(PhysicsComponent))
            {
                PhysicsMessage message = PhysicsMessage.Deserialize(snapshot.Payload);
                localPlayerReconciler.ApplyPhysics(
                    store,
                    entity,
                    new Vector3(message.VelocityX, message.VelocityY, message.VelocityZ),
                    new Vector3(message.AngularVelocityX, message.AngularVelocityY, message.AngularVelocityZ)
                );
            }
            else
            {
                ApplyCodec(store, entity, in info, snapshot.Payload);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Dropping {component} snapshot for entity {entity}.", info.Type.Name, entity);
        }
    }

    private void ApplyCodec(DataStore store, int entity, in NetworkComponentInfo info, ReadOnlySpan<byte> payload)
    {
        Result applied = info.Codec.Apply(store, entity, payload);
        if (!applied.Success)
        {
            logger.LogWarning(
                "Dropping {component} snapshot for entity {entity}: {message}.",
                info.Type.Name, entity, applied.Message);
        }
    }

    /// <summary>
    /// Applies every component of a superseded burst frame except the continuously-dirty motion pair,
    /// so one-shot server state seeded early in the join burst (the starter inventory, seeded context)
    /// survives the entering-play coalesce. The local player's spawn is seeded at join, so a dropped
    /// motion frame cannot lose its seat.
    /// </summary>
    private void ApplyOneShotComponents(WorldSnapshot snapshot, DataStore store)
    {
        ComponentSnapshot[] components = snapshot.Components;
        for (var i = 0; i < components.Length; i++)
        {
            Result<NetworkComponentInfo> lookup = registry.GetInfo(Uuid.FromValue(components[i].TypeUuid));
            if (!lookup.Success
                || lookup.Value.Type == typeof(TransformComponent)
                || lookup.Value.Type == typeof(PhysicsComponent))
            {
                continue;
            }

            ApplyComponent(components[i], store);
        }
    }

    private static void TrimPendingInput(uint ackTick, DataStore store)
    {
        TrimPendingAction action = new() { AckTick = ackTick };
        store.Query<PlayerComponent, PendingInputComponent, TrimPendingAction>(0f, ref action);
    }

    private struct TrimPendingAction : IForEach<PlayerComponent, PendingInputComponent>
    {
        public uint AckTick;

        /// <inheritdoc/>
        public void Execute(float delta, DataStore store, int entity, in PlayerComponent player, in PendingInputComponent pending)
        {
            pending.Ring?.TrimAtOrBelow(AckTick);
        }
    }
}
