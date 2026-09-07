using System;
using System.Collections.Generic;
using System.Numerics;
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

    //  Local players whose look is now client-predicted. The first authoritative transform seats them
    //  (server-assigned spawn); afterwards their orientation comes from local prediction, not the echo,
    //  so the authoritative snapshot never snaps the view back and causes look jitter.
    private readonly HashSet<Uuid> _seatedPlayers = [];

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

        if (!store.TryGet(entity, out PlayerComponent _))
        {
            info.Codec.Apply(store, entity, snapshot.Payload);
            return;
        }

        //  The local player's look is client-predicted; never accept the authoritative echo over it.
        //  Position/scale (and linear velocity) are authority, but orientation and angular velocity stay
        //  locally predicted once seated at spawn, so the echo never snaps or clobbers the local look.
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
            info.Codec.Apply(store, entity, snapshot.Payload);
        }
    }

    private void ApplyLocallyOwnedTransform(DataStore store, int entity, ReadOnlySpan<byte> payload)
    {
        TransformMessage message = TransformMessage.Deserialize(payload);

        var serverPosition = new Vector3(message.PositionX, message.PositionY, message.PositionZ);
        var serverScale = new Vector3(message.ScaleX, message.ScaleY, message.ScaleZ);

        if (_seatedPlayers.Add(store.GetUuid(entity)))
        {
            //  Seed the server-assigned spawn transform once.
            store.AddOrUpdate(entity, new TransformComponent(
                serverPosition,
                new Quaternion(message.OrientationX, message.OrientationY, message.OrientationZ, message.OrientationW),
                serverScale
            ));
            return;
        }

        //  Keep the locally predicted look; snap only position/scale from the authority.
        store.QueryRef<TransformComponent>(entity, 0f, (float _, DataStore s, int e, ref Ref<TransformComponent> transform) =>
        {
            ref TransformComponent transformValue = ref transform.Write;
            transformValue.Position = serverPosition;
            transformValue.Scale = serverScale;
        });
    }

    private static void ApplyLocallyOwnedPhysics(DataStore store, int entity, ReadOnlySpan<byte> payload)
    {
        //  Linear velocity is authority; angular velocity is the local torque-driven look and must not be
        //  clobbered by the server echo.
        PhysicsMessage message = PhysicsMessage.Deserialize(payload);

        store.QueryRef<PhysicsComponent>(entity, 0f, (float _, DataStore s, int e, ref Ref<PhysicsComponent> physics) =>
        {
            physics.Write.Velocity = new Vector3(message.VelocityX, message.VelocityY, message.VelocityZ);
        });
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