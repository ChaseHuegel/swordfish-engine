using System;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Numerics;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Transport;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// Reconciles the client's predicted voxel edits against the server's authoritative
/// <see cref="VoxelEditMessage"/>s. Each edit is correlated with the local player's
/// <see cref="PendingInteractionQueue"/> by (entity, coordinate): a matching echo confirms the prediction
/// (no-op, already applied), a differing echo snaps it to authority, and a prediction that ages past a
/// bound with no echo (the server rejected it) is reverted to the pre-prediction voxel. Applied edits
/// publish via the voxel component's dirty flag, which the <see cref="VoxelEntityRebuildSystem"/>
/// consumes to rebuild the mesh/collider. Unpredicted (remote or authoritative) edits play the matching
/// break/place sound; edits correlating to the local player's own predictions play none - the prediction
/// already sounded. Gated on <see cref="GameState.Playing"/> so it never races the
/// load-thread world build.
/// </summary>
internal sealed class ClientVoxelReconcileSystem : IEntitySystem
{
    /// <summary>Sim ticks to wait past an interaction's sample before reverting a still-unconfirmed prediction.</summary>
    private const int REVERT_BOUND_SIM_TICKS = 40;

    private readonly IClientConnection _transport;
    private readonly SnapshotAckTracker _snapshotAck;
    private readonly IBrickIdMap _brickIdMap;
    private readonly SoundEffectService _soundEffectService;
    private readonly IBrickDatabase _brickDatabase;

    public ClientVoxelReconcileSystem(
        in IClientConnection transport,
        in SnapshotAckTracker snapshotAck,
        IBrickIdMap brickIdMap,
        in SoundEffectService soundEffectService,
        in IBrickDatabase brickDatabase
    ) {
        _transport = transport;
        _snapshotAck = snapshotAck;
        _brickIdMap = brickIdMap;
        _soundEffectService = soundEffectService;
        _brickDatabase = brickDatabase;
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
            if (VoxelEquals(in edit.Predicted, in message.Voxel, message.BrickId))
            {
                //  The server agreed with the prediction - already applied. Resolve the pending entry.
                queue!.Remove(edit.Entity, edit.Coordinate);
                return;
            }

            //  The server's authoritative voxel differs from the prediction - snap to authority. The
            //  prediction already sounded; snap plays none.
            queue!.Remove(edit.Entity, edit.Coordinate);
        }

        //  The echo carries the server's voxel id; resolve it to this process's local id space for the write.
        Voxel authority = message.Voxel;
        if (!string.IsNullOrEmpty(message.BrickId))
        {
            authority.ID = _brickIdMap.Id(message.BrickId);
        }

        //  The pre-edit voxel survives only for an unpredicted edit's sound material: a break's result is
        //  empty, so the broken brick's material must come from the cell before the write.
        Voxel previous = default;
        if (pending == null && store.TryGet(entity, out VoxelComponent previousComponent))
        {
            previous = previousComponent.VoxelObject.Get(coordinate.X, coordinate.Y, coordinate.Z);
        }

        WriteVoxel(store, entity, in coordinate, in authority);

        //  An unpredicted (remote or authoritative) edit is audible; an own prediction's echo is not -
        //  the prediction already played its sound.
        if (pending == null)
        {
            PlayEditSound(in previous, in authority);
        }
    }

    /// <summary>
    /// Plays the break or place sound for an unpredicted authoritative edit, with the material-class
    /// variant (rock vs metal) resolved from the brick's tags, mirroring the prediction path. The result
    /// voxel decides break (empty) vs place (filled); the material comes from the pre-edit voxel for a
    /// break and the result voxel for a place.
    /// </summary>
    private void PlayEditSound(in Voxel previous, in Voxel result)
    {
        bool isBreak = result.ID == 0;
        Voxel materialVoxel = isBreak ? previous : result;

        bool isRock = false;
        if (materialVoxel.ID != 0 && _brickDatabase.Get(materialVoxel.ID) is { Success: true } info)
        {
            isRock = info.Value.Tags.Contains("environment");
        }

        if (isBreak)
        {
            if (isRock)
            {
                _soundEffectService.PlayRemoveRock();
            }
            else
            {
                _soundEffectService.PlayRemoveMetal();
            }
            return;
        }

        if (isRock)
        {
            _soundEffectService.PlayPlaceRock();
        }
        else
        {
            _soundEffectService.PlayPlaceMetal();
        }
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

    private bool VoxelEquals(in Voxel predicted, in Voxel server, string? serverBrickId)
    {
        if (predicted.ShapeLight != server.ShapeLight || predicted.Orientation != server.Orientation)
        {
            return false;
        }

        //  Compare by canonical brick name so confirmation holds even when the client and server resolve
        //  different numeric ids for the same brick. Fall back to the raw id when the echo carries no name.
        if (!string.IsNullOrEmpty(serverBrickId))
        {
            return _brickIdMap.Name(predicted.ID) == serverBrickId;
        }

        return predicted.ID == server.ID;
    }
}