using System;
using System.Collections.Generic;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Components;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Networking.Codecs;
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
    //  Remote bodies correct like the local player: ignore sub-centimeter drift, blend larger errors, and
    //  snap past the correction distance so a respawn or teleport does not glide across the world.
    private const float REMOTE_POSITION_CORRECTION_BLEND = 0.5f;
    private const float REMOTE_POSITION_DEADBAND_SQ = 0.0001f;
    private const float REMOTE_POSITION_SNAP_DISTANCE_SQ =
        CharacterDefaults.RECONCILE_SNAP_DISTANCE * CharacterDefaults.RECONCILE_SNAP_DISTANCE;

    private readonly FragmentAssembler _snapshotAssembler = new();
    private readonly List<WorldSnapshot> _snapshots = [];
    private readonly Dictionary<ulong, int> _latestTransformSnapshots = [];

    private bool _wasBelowPlaying = true;
    private bool _hasAlignedToSnapshot;

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
            _hasAlignedToSnapshot = false;
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
                //  inventory or stationary entity transforms) rides only early frames and must not be dropped,
                //  so each frame that is superseded applies non-motion components plus any final non-player
                //  transform absent from later frames; the newest applies fully and wins on moving entities.
                if (_snapshots.Count > 1)
                {
                    CollectLatestTransformSnapshots();
                    for (var index = 0; index < _snapshots.Count - 1; index++)
                    {
                        ApplyOneShotComponents(_snapshots[index], index, store);
                    }

                    _latestTransformSnapshots.Clear();
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
        //  Despawns apply before components so a same-tick despawn and reallocation of one uuid
        //  frees the old entity before the new entity's components allocate and populate it. The
        //  server collects components only from live entities, so a true despawn carries no
        //  component snapshots that could resurrect it.
        ulong[] removed = snapshot.RemovedEntities ?? [];
        for (var i = 0; i < removed.Length; i++)
        {
            if (!store.TryGet(Uuid.FromValue(removed[i]), out int entity))
            {
                continue;
            }

            //  The local player is owned by the client session lifecycle, so a same-tick rejoin
            //  despawn never frees the freshly seated local entity.
            if (store.TryGet(entity, out PlayerComponent _))
            {
                continue;
            }

            //  A brick entity that also has local client visuals (ChildComponent + BrickAnchorComponent)
            //  stays alive until VoxelEntityBuilder reconciles the removed voxel; strip only server state.
            if (store.TryGet(entity, out ChildComponent _) && store.TryGet(entity, out BrickAnchorComponent _))
            {
                store.Remove<InventoryComponent>(entity);
                store.Remove<MachineStateComponent>(entity);
                continue;
            }

            if (store.TryGet(entity, out VoxelComponent _))
            {
                VoxelEntityBuilder.Destroy(store, entity);
                continue;
            }

            //  Free captures every component removal; the engine releases their native resources.
            store.Free(entity);
        }

        ComponentSnapshot[] components = snapshot.Components;
        for (var i = 0; i < components.Length; i++)
        {
            ApplyComponent(components[i], store);
        }

        //  Component removals apply after components so a removal in the same snapshot can never be
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

        //  Derive remote character colliders locally from BodyViewComponent so ColliderComponent never
        //  needs to cross the wire or overwrite the local player's or voxel structures' colliders.
        EnsureRemoteCharacterColliders(store);

        //  The pending ring has not been trimmed yet, so the reconciler can read the look totals at the
        //  server's ack before the trim removes them.
        localPlayerReconciler.ApplySnapshot(store, snapshot.LastProcessedInput);

        TrimPendingInput(snapshot.LastProcessedInput, snapshot.TickNumber, rebaseClock: !_hasAlignedToSnapshot, store);

        //  Align live prediction with the server's sim tick after the authoritative state is applied.
        motionProcessor.Step?.AlignTo(snapshot.TickNumber);
        _hasAlignedToSnapshot = true;

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
            //  A remote player has no client-authoritative state: its body is simulated locally and
            //  corrected by the snapshot. Blend the position so that correction does not snap each
            //  authoritative frame. Orientation and scale stay authoritative.
            if (info.Type == typeof(TransformComponent) && store.TryGet(entity, out BodyViewComponent _))
            {
                try
                {
                    TransformMessage message = TransformMessage.Deserialize(snapshot.Payload);
                    ApplyRemoteTransform(
                        store,
                        entity,
                        new Vector3(message.PositionX, message.PositionY, message.PositionZ),
                        new Quaternion(message.OrientationX, message.OrientationY, message.OrientationZ, message.OrientationW),
                        new Vector3(message.ScaleX, message.ScaleY, message.ScaleZ)
                    );
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Dropping {component} snapshot for entity {entity}.", info.Type.Name, entity);
                }

                return;
            }

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

    private void ApplyRemoteTransform(
        DataStore store,
        int entity,
        in Vector3 position,
        in Quaternion orientation,
        in Vector3 scale
    ) {
        //  A remote body is simulated locally, so a bad snapshot must not reach the solver: keep the
        //  last good transform and drop the frame.
        if (!TransformCodec.IsValid(in position, in orientation, in scale))
        {
            logger.LogWarning(
                "Dropping {component} snapshot for entity {entity}: the transform is non-finite or its orientation is degenerate.",
                nameof(TransformComponent), entity);
            return;
        }

        if (!store.TryGet(entity, out TransformComponent _))
        {
            store.AddOrUpdate(entity, new TransformComponent(position, orientation, scale));
            return;
        }

        var write = new BlendRemoteTransform(in position, in orientation, in scale);
        store.QueryRef<TransformComponent, BlendRemoteTransform>(entity, 0f, ref write);
    }

    private readonly struct BlendRemoteTransform(
        in Vector3 position,
        in Quaternion orientation,
        in Vector3 scale
    ) : IForEachRef<TransformComponent>
    {
        private readonly Vector3 _position = position;
        private readonly Quaternion _orientation = orientation;
        private readonly Vector3 _scale = scale;

        public void Execute(float delta, DataStore store, int entity, ref Ref<TransformComponent> transform)
        {
            ref readonly TransformComponent current = ref transform.Read;
            Vector3 offset = _position - current.Position;
            float offsetSq = offset.LengthSquared();
            bool positionChanged = offsetSq > REMOTE_POSITION_DEADBAND_SQ;
            bool poseChanged = current.Orientation != _orientation || current.Scale != _scale;
            if (!positionChanged && !poseChanged)
            {
                return;
            }

            ref TransformComponent value = ref transform.Write;
            if (positionChanged)
            {
                value.Position = offsetSq > REMOTE_POSITION_SNAP_DISTANCE_SQ
                    ? _position
                    : current.Position + offset * REMOTE_POSITION_CORRECTION_BLEND;
            }

            value.Orientation = _orientation;
            value.Scale = _scale;
        }
    }

    private void CollectLatestTransformSnapshots()
    {
        _latestTransformSnapshots.Clear();
        for (var snapshotIndex = 0; snapshotIndex < _snapshots.Count; snapshotIndex++)
        {
            ComponentSnapshot[] components = _snapshots[snapshotIndex].Components;
            for (var i = 0; i < components.Length; i++)
            {
                Result<NetworkComponentInfo> lookup = registry.GetInfo(Uuid.FromValue(components[i].TypeUuid));
                if (lookup.Success && lookup.Value.Type == typeof(TransformComponent))
                {
                    _latestTransformSnapshots[components[i].Entity] = snapshotIndex;
                }
            }
        }
    }

    /// <summary>
    /// Applies every component of a superseded burst frame except velocity physics and superseded or
    /// local-player transforms, so one-shot server state and stationary world-space entity transforms
    /// seeded early in the join burst survive the entering-play coalesce.
    /// </summary>
    private void ApplyOneShotComponents(WorldSnapshot snapshot, int snapshotIndex, DataStore store)
    {
        ComponentSnapshot[] components = snapshot.Components;
        for (var i = 0; i < components.Length; i++)
        {
            Result<NetworkComponentInfo> lookup = registry.GetInfo(Uuid.FromValue(components[i].TypeUuid));
            if (!lookup.Success || lookup.Value.Type == typeof(PhysicsComponent))
            {
                continue;
            }

            if (lookup.Value.Type == typeof(TransformComponent))
            {
                bool isNewestTransformForEntity = _latestTransformSnapshots.TryGetValue(components[i].Entity, out int newestIndex)
                    && newestIndex == snapshotIndex;
                bool isLocalPlayer = store.TryGet(Uuid.FromValue(components[i].Entity), out int existingEntity)
                    && store.Has(typeof(PlayerComponent), existingEntity);
                if (!isNewestTransformForEntity || isLocalPlayer)
                {
                    continue;
                }
            }

            ApplyComponent(components[i], store);
        }
    }

    private static void EnsureRemoteCharacterColliders(DataStore store)
    {
        EnsureRemoteCharacterColliderAction action = new();
        store.Query<BodyViewComponent, EnsureRemoteCharacterColliderAction>(0f, ref action);
    }

    private struct EnsureRemoteCharacterColliderAction : IForEach<BodyViewComponent>
    {
        /// <inheritdoc/>
        public void Execute(float delta, DataStore store, int entity, in BodyViewComponent body)
        {
            if (store.Has(typeof(PlayerComponent), entity) || store.Has(typeof(ColliderComponent), entity))
            {
                return;
            }

            Vector3 scale = CharacterDefaults.PlayerScale;
            if (store.TryGet(entity, out TransformComponent transform)
                && float.IsFinite(transform.Scale.X)
                && float.IsFinite(transform.Scale.Y)
                && float.IsFinite(transform.Scale.Z)
                && transform.Scale.X > 0f
                && transform.Scale.Y > 0f
                && transform.Scale.Z > 0f)
            {
                scale = transform.Scale;
            }

            store.AddOrUpdate(entity, CharacterDefaults.CreateCharacterColliderComponent(scale));
        }
    }

    private static void TrimPendingInput(uint ackTick, uint snapshotTick, bool rebaseClock, DataStore store)
    {
        TrimPendingAction action = new()
        {
            AckTick = ackTick,
            SnapshotTick = snapshotTick,
            RebaseClock = rebaseClock,
        };
        store.QueryRef<PlayerComponent, PendingInputComponent, TrimPendingAction>(0f, ref action);
    }

    private struct TrimPendingAction : IForEachRef<PlayerComponent, PendingInputComponent>
    {
        public uint AckTick;
        public uint SnapshotTick;
        public bool RebaseClock;

        /// <inheritdoc/>
        public void Execute(float delta, DataStore store, int entity, ref Ref<PlayerComponent> player, ref Ref<PendingInputComponent> pending)
        {
            StageRing<StagedInput>? ring = pending.Read.Ring;
            if (ring == null)
            {
                return;
            }

            StagedInput? trimmed = ring.TrimAtOrBelow(AckTick);
            if (RebaseClock)
            {
                for (var index = 0; index < ring.Count; index++)
                {
                    StagedInput entry = ring[index];
                    if (entry.StagedAtSimTick < SnapshotTick)
                    {
                        ring[index] = new StagedInput(in entry.Input, SnapshotTick);
                    }
                }
            }

            if (trimmed != null)
            {
                StagedInput acked = trimmed.Value;
                if (RebaseClock && acked.StagedAtSimTick < SnapshotTick)
                {
                    acked = new StagedInput(in acked.Input, SnapshotTick);
                }

                pending.Write.LastAcked = acked;
            }
        }
    }
}
