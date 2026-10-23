using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Networking.Rpc;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// Client-side reconciliation. Applies authoritative server-owned component snapshots (full state:
/// position, orientation, linear + angular velocity), trims <see cref="PendingInputComponent"/> up to
/// the server's last-processed sim tick, realigns the shared prediction step to the server's published
/// sim tick, and frees entities despawned by the server. Local prediction continues from the corrected
/// state, so concatenated snapshots never over-apply commands the server already collapsed.
/// The transition into <c>Playing</c> coalesces any queued snapshot burst to the newest frame so a
/// Loading-time backlog can never hitch the first play tick.
/// </summary>
internal sealed class ClientReconcileSystem : IEntitySystem
{
    private readonly ClientInbound _inbound;
    private readonly SnapshotAckTracker _snapshotAck;
    private readonly ClientPlayerMotionProcessor _motionProcessor;
    private readonly NetworkRegistry _registry;
    private readonly ILogger<ClientReconcileSystem> _logger;
    private readonly FragmentAssembler _snapshotAssembler = new();
    private readonly List<WorldSnapshot> _snapshots = [];

    //  Local players whose look is now client-predicted. The first authoritative transform seats them
    //  (server-assigned spawn); afterwards their orientation comes from local prediction, not the echo,
    //  so the authoritative snapshot never snaps the view back and causes look jitter.
    private readonly HashSet<Uuid> _seatedPlayers = [];

    private bool _wasBelowPlaying = true;

    public ClientReconcileSystem(
        in ClientInbound inbound,
        SnapshotAckTracker snapshotAck,
        in ClientPlayerMotionProcessor motionProcessor,
        NetworkRegistry registry,
        ILogger<ClientReconcileSystem> logger
    ) {
        _inbound = inbound;
        _snapshotAck = snapshotAck;
        _motionProcessor = motionProcessor;
        _registry = registry;
        _logger = logger;
    }

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
            return;
        }

        DrainSnapshots();
        if (_snapshots.Count == 0)
        {
            return;
        }

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
            return;
        }

        foreach (WorldSnapshot snapshot in _snapshots)
        {
            //  Re-check the gate inside the drain so an exit to the menu mid-drain stops applying (and
            //  can no longer re-allocate entities the teardown is freeing).
            if (WaywardBeyond.GameState < GameState.Playing)
            {
                return;
            }

            ApplySnapshot(snapshot, store);
        }
    }

    /// <summary>
    /// Drains both snapshot queues and reassembles complete batches. Snapshots apply in tick order, so
    /// a full frame and a later batch never invert. An incomplete batch waits for its next part.
    /// </summary>
    private void DrainSnapshots()
    {
        _snapshots.Clear();

        while (_inbound.Snapshots.TryDequeue(out ClientInbound.InboundSnapshot message))
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
                    _logger.LogWarning("Dropping a snapshot batch with an invalid fragment.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Dropping a malformed snapshot.");
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
            Result<NetworkComponentInfo> lookup = _registry.GetInfo(Uuid.FromValue(removal.TypeUuid));
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

        Result<NetworkComponentInfo> lookup = _registry.GetInfo(Uuid.FromValue(snapshot.TypeUuid));
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

        //  The local player's look is client-predicted; never accept the authoritative echo over it.
        //  Position/scale (and linear velocity) are authority, but orientation and angular velocity stay
        //  locally predicted once seated at spawn, so the echo never snaps or clobbers the local look.
        try
        {
            if (info.Type == typeof(TransformComponent))
            {
                ApplyLocallyOwnedTransform(store, entity, snapshot.Payload);
            }
            else if (info.Type == typeof(PhysicsComponent))
            {
                ApplyLocallyOwnedPhysics(store, entity, snapshot.Payload);
            }
            else
            {
                ApplyCodec(store, entity, in info, snapshot.Payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dropping {component} snapshot for entity {entity}.", info.Type.Name, entity);
        }
    }

    private void ApplyCodec(DataStore store, int entity, in NetworkComponentInfo info, ReadOnlySpan<byte> payload)
    {
        Result applied = info.Codec.Apply(store, entity, payload);
        if (!applied.Success)
        {
            _logger.LogWarning(
                "Dropping {component} snapshot for entity {entity}: {message}.",
                info.Type.Name, entity, applied.Message);
        }
    }

    /// <summary>
    /// Applies every component of a superseded burst frame except the continuously-dirty motion pair,
    /// so one-shot server state seeded early in the join burst (the starter inventory, seeded context)
    /// survives the entering-play coalesce.
    /// </summary>
    private void ApplyOneShotComponents(WorldSnapshot snapshot, DataStore store)
    {
        ComponentSnapshot[] components = snapshot.Components;
        for (var i = 0; i < components.Length; i++)
        {
            Result<NetworkComponentInfo> lookup = _registry.GetInfo(Uuid.FromValue(components[i].TypeUuid));
            if (!lookup.Success
                || lookup.Value.Type == typeof(TransformComponent)
                || lookup.Value.Type == typeof(PhysicsComponent))
            {
                continue;
            }

            ApplyComponent(components[i], store);
        }
    }

    private void ApplyLocallyOwnedTransform(DataStore store, int entity, ReadOnlySpan<byte> payload)
    {
        TransformMessage message = TransformMessage.Deserialize(payload);

        var serverPosition = new Vector3(message.PositionX, message.PositionY, message.PositionZ);
        var serverOrientation = new Quaternion(message.OrientationX, message.OrientationY, message.OrientationZ, message.OrientationW);
        var serverScale = new Vector3(message.ScaleX, message.ScaleY, message.ScaleZ);

        if (_seatedPlayers.Add(store.GetUuid(entity)))
        {
            //  Seed the server-assigned spawn transform once.
            store.AddOrUpdate(entity, new TransformComponent(
                serverPosition,
                serverOrientation,
                serverScale
            ));
            return;
        }

        //  Keep the locally predicted look; snap only position/scale from the authority.
        var write = new SeatTransform(in serverPosition, in serverScale);
        store.QueryRef<TransformComponent, SeatTransform>(entity, 0f, ref write);
    }

    private struct SeatTransform(in Vector3 position, in Vector3 scale) : IForEachRef<TransformComponent>
    {
        private readonly Vector3 _position = position;
        private readonly Vector3 _scale = scale;

        public void Execute(float delta, DataStore store, int entity, ref Ref<TransformComponent> transform)
        {
            ref readonly TransformComponent current = ref transform.Read;
            if (current.Position == _position && current.Scale == _scale)
            {
                return;
            }

            ref TransformComponent value = ref transform.Write;
            value.Position = _position;
            value.Scale = _scale;
        }
    }

    private static void ApplyLocallyOwnedPhysics(DataStore store, int entity, ReadOnlySpan<byte> payload)
    {
        //  Linear velocity is authority; angular velocity is the local torque-driven look and must not be
        //  clobbered by the server echo.
        PhysicsMessage message = PhysicsMessage.Deserialize(payload);
        var write = new SeatVelocity(new Vector3(message.VelocityX, message.VelocityY, message.VelocityZ));
        store.QueryRef<PhysicsComponent, SeatVelocity>(entity, 0f, ref write);
    }

    private struct SeatVelocity(in Vector3 velocity) : IForEachRef<PhysicsComponent>
    {
        private readonly Vector3 _velocity = velocity;

        public void Execute(float delta, DataStore store, int entity, ref Ref<PhysicsComponent> physics)
        {
            ref readonly PhysicsComponent current = ref physics.Read;
            if (current.Velocity == _velocity)
            {
                return;
            }

            physics.Write.Velocity = _velocity;
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

        public void Execute(float delta, DataStore store, int entity, in PlayerComponent player, in PendingInputComponent pending)
        {
            pending.Ring?.TrimAtOrBelow(AckTick);
        }
    }
}