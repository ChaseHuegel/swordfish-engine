using System;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Networking;
using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Reconciles the client's predicted voxel edits against the server's authoritative
/// <see cref="VoxelEditMessage"/>s. Each edit is correlated with the local player's
/// <see cref="PendingInteractionQueue"/> by (entity, coordinate): a matching echo confirms the prediction
/// (no-op, already applied), a differing echo snaps it to authority, and a prediction that ages past a
/// bound with no echo (the server rejected it) is reverted to the pre-prediction voxel. Applied edits
/// publish via the voxel component's dirty flag, which the <see cref="VoxelEntityRebuildSystem"/>
/// consumes to rebuild the mesh/collider. Gated on <see cref="GameState.Playing"/> so it never races the
/// load-thread world build.
/// </summary>
internal sealed class ClientVoxelReconcileSystem : IEntitySystem
{
    /// <summary>Sim ticks to wait past an interaction's sample before reverting a still-unconfirmed prediction.</summary>
    private const int REVERT_BOUND_SIM_TICKS = 40;

    private readonly IClientConnection _transport;
    private readonly SnapshotAckTracker _snapshotAck;

    public ClientVoxelReconcileSystem(
        in IClientConnection transport,
        in SnapshotAckTracker snapshotAck
    ) {
        _transport = transport;
        _snapshotAck = snapshotAck;
    }

    public void Tick(float delta, DataStore store)
    {
        //  Authoritative edits only apply once play has begun; during Loading the load thread builds view
        //  entities and writing into those entities concurrently would race it.
        if (WaywardBeyond.GameState < GameState.Playing)
        {
            return;
        }

        int localPlayer = -1;
        store.Query<PlayerComponent>(0f, (float _, DataStore s, int e, in PlayerComponent playerComponent) => localPlayer = e);

        PendingInteractionQueue? queue = null;
        if (localPlayer >= 0 && store.TryGet(localPlayer, out PendingInteractionComponent pendingComponent))
        {
            queue = pendingComponent.Queue;
        }

        Result<VoxelEditMessage> receiveResult;
        while ((receiveResult = _transport.Receive<VoxelEditMessage>()).Success)
        {
            if (WaywardBeyond.GameState < GameState.Playing)
            {
                return;
            }

            ApplyAuthoritativeEdit(store, receiveResult.Value, queue);
        }

        if (queue != null)
        {
            RevertExpiredPredictions(store, queue);
        }
    }

    private void ApplyAuthoritativeEdit(DataStore store, in VoxelEditMessage message, PendingInteractionQueue? queue)
    {
        if (!store.TryGet(Uuid.FromValue(message.EntityUuid), out int entity) ||
            !store.TryGet(entity, out VoxelComponent _))
        {
            return;
        }

        Int3 coordinate = new(message.X, message.Y, message.Z);

        //  Correlate the echo to the exact prediction by its interaction sequence: a discrete edge is
        //  uniquely addressed, so a late echo resolves the right pending entry even if another edit has
        //  since touched the same cell. Falls back to (entity, coordinate) when the echo carries no
        //  sequence (retransmitted/stale edit from before this field existed).
        PendingEdit? pending = FindPending(queue, message.Sequence, entity, in coordinate);
        if (pending != null)
        {
            PendingEdit edit = pending.Value;
            if (VoxelEquals(in edit.Predicted, in message.Voxel))
            {
                //  The server agreed with the prediction - already applied. Resolve the pending entry.
                queue!.Remove(edit.Entity, edit.Coordinate);
                return;
            }

            //  The server's authoritative voxel differs from the prediction - snap to authority.
            queue!.Remove(edit.Entity, edit.Coordinate);
        }

        WriteVoxel(store, entity, in coordinate, in message.Voxel);
    }

    private static PendingEdit? FindPending(
        PendingInteractionQueue? queue,
        uint sequence,
        int entity,
        in Int3 coordinate
    ) {
        if (queue == null)
        {
            return null;
        }

        if (sequence != 0 && queue.TryFindBySequence(sequence, out PendingEdit bySequence))
        {
            return bySequence;
        }

        if (queue.TryFind(entity, coordinate, out PendingEdit byCoordinate))
        {
            return byCoordinate;
        }

        return null;
    }

    private void RevertExpiredPredictions(DataStore store, PendingInteractionQueue queue)
    {
        //  The server's last processed sim tick; once a prediction has been outstanding far past its
        //  sample tick with no echo, the server rejected it - restore the pre-prediction voxel.
        uint currentTick = _snapshotAck.LastAppliedSnapshotTick;
        if (currentTick == 0)
        {
            return;
        }

        PendingEdit[] edits = queue.Snapshot();
        for (var i = 0; i < edits.Length; i++)
        {
            PendingEdit edit = edits[i];
            if (currentTick <= edit.ServerTickAtSample || currentTick - edit.ServerTickAtSample <= REVERT_BOUND_SIM_TICKS)
            {
                continue;
            }

            if (queue.Remove(edit.Entity, edit.Coordinate))
            {
                WriteVoxel(store, edit.Entity, in edit.Coordinate, in edit.Original);
            }
        }
    }

    private void WriteVoxel(DataStore store, int entity, in Int3 coordinate, in Voxel voxel)
    {
        if (!store.TryGet(entity, out VoxelComponent voxelComponent))
        {
            return;
        }

        voxelComponent.VoxelObject.Set(coordinate.X, coordinate.Y, coordinate.Z, voxel);

        //  Publish the edit by marking the voxel component dirty; the VoxelEntityRebuildSystem observes
        //  this flag and fulfills the mesh/collider rebuild on the ECS thread.
        store.MarkDirty<VoxelComponent>(entity);
    }

    private static bool VoxelEquals(in Voxel a, in Voxel b)
    {
        return a.ID == b.ID && a.ShapeLight == b.ShapeLight && a.Orientation == b.Orientation;
    }
}