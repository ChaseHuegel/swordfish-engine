using Krill;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Voxels.Processing;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// Reconciles the client's predicted voxel edits against the server's authoritative
/// <see cref="VoxelEditMessage"/>s. Each edit is correlated with the local player's
/// <see cref="PendingInteractionQueue"/> by (entity, coordinate): a matching echo confirms the prediction
/// (no-op, already applied), a differing echo snaps it to authority, and a prediction that ages past a
/// bound with no echo (the server rejected it) is reverted to the pre-prediction voxel. Applied edits
/// publish via the voxel component's dirty flag, which the <see cref="VoxelEntityRebuildSystem"/>
/// consumes to rebuild the mesh/collider. Unpredicted (remote or authoritative) edits raise
/// <see cref="BrickEvent"/>; edits correlating to the local player's own predictions do not.
/// Gated on <see cref="GameState.Playing"/> so it never races the load-thread world build.
/// </summary>
internal sealed class ClientVoxelReconcileSystem : IEntitySystem
{
    /// <summary>Sim ticks to wait past an interaction's sample before reverting a still-unconfirmed prediction.</summary>
    private const int REVERT_BOUND_SIM_TICKS = 40;

    private readonly ClientInbound _inbound;
    private readonly SnapshotAckTracker _snapshotAck;
    private readonly IBrickRegistry _brickRegistry;
    private readonly IPaintRegistry? _paintRegistry;
    private readonly VoxelLightingService _voxelLightingService;
    private readonly EventInvoker<BrickEvent> _brickEvent;

    public ClientVoxelReconcileSystem(
        in ClientInbound inbound,
        in SnapshotAckTracker snapshotAck,
        IBrickRegistry brickRegistry,
        in VoxelLightingService voxelLightingService,
        in EventInvoker<BrickEvent> brickEvent,
        IPaintRegistry? paintRegistry = null
    ) {
        _inbound = inbound;
        _snapshotAck = snapshotAck;
        _brickRegistry = brickRegistry;
        _paintRegistry = paintRegistry;
        _voxelLightingService = voxelLightingService;
        _brickEvent = brickEvent;
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

        while (_inbound.VoxelEdits.TryDequeue(out VoxelEditMessage message))
        {
            if (WaywardBeyond.GameState < GameState.Playing)
            {
                return;
            }

            ApplyAuthoritativeEdit(store, message, queue);
        }

        while (_inbound.VoxelFaceEdits.TryDequeue(out VoxelFaceEditMessage faceMessage))
        {
            if (WaywardBeyond.GameState < GameState.Playing)
            {
                return;
            }

            ApplyAuthoritativeFaceEdit(store, in faceMessage, queue);
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
        queue?.RemoveFaceEditsForCell(entity, in coordinate, message.Sequence);

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
            authority.ID = _brickRegistry.Resolve(message.BrickId);
        }

        //  The pre-edit voxel survives only for an unpredicted edit's presentation event: a break's result
        //  is empty, so the broken brick id must come from the cell before the write.
        Voxel previous = default;
        if (pending == null && store.TryGet(entity, out VoxelComponent previousComponent))
        {
            previous = previousComponent.VoxelObject.Get(coordinate.X, coordinate.Y, coordinate.Z);
        }

        WriteVoxel(store, entity, in coordinate, in authority);

        //  An unpredicted (remote or authoritative) edit raises the presentation event; an own prediction's
        //  echo does not because the prediction path already raised it.
        if (pending == null)
        {
            RaiseEditEvent(in previous, in authority);
        }
    }

    /// <summary>
    /// Raises <see cref="BrickEvent"/> for an unpredicted authoritative edit. The result voxel decides
    /// break (empty) vs place (filled); the brick id comes from the pre-edit voxel for a break and the
    /// result voxel for a place.
    /// </summary>
    private void RaiseEditEvent(in Voxel previous, in Voxel result)
    {
        bool isBreak = result.ID == 0;
        ushort brickID = isBreak ? previous.ID : result.ID;
        if (brickID == 0)
        {
            return;
        }

        _brickEvent.Invoke(new BrickEvent(brickID, isBreak ? BrickEventKind.Break : BrickEventKind.Place));
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

        PendingFaceEdit[] faceEdits = queue.SnapshotFaceEdits();
        for (var i = 0; i < faceEdits.Length; i++)
        {
            PendingFaceEdit edit = faceEdits[i];
            if (currentTick <= edit.ServerTickAtSample || currentTick - edit.ServerTickAtSample <= REVERT_BOUND_SIM_TICKS)
            {
                continue;
            }

            if (queue.RemoveFaceEdit(edit.Sequence))
            {
                RestoreFaceOverrides(store, edit.Entity, in edit.Coordinate, edit.FaceMask, edit.OriginalOverrides);
            }
        }
    }

    private void ApplyAuthoritativeFaceEdit(DataStore store, in VoxelFaceEditMessage message, PendingInteractionQueue? queue)
    {
        if (!store.TryGet(Uuid.FromValue(message.EntityUuid), out int entity) ||
            !store.TryGet(entity, out VoxelComponent voxelComponent))
        {
            return;
        }

        if (message.Mode > (byte)PaintKind.Color)
        {
            return;
        }

        Int3 coordinate = new(message.X, message.Y, message.Z);
        var faceMask = (BrickFaceMask)message.FaceMask;
        var mode = (PaintKind)message.Mode;
        string? serverPaintId = string.IsNullOrEmpty(message.PaintId) ? null : message.PaintId;

        ushort localPaintId = serverPaintId != null && _paintRegistry != null
            ? _paintRegistry.Resolve(serverPaintId)
            : (ushort)0;

        if (queue != null && message.Sequence != 0 && queue.TryFindFaceEditBySequence(message.Sequence, out PendingFaceEdit pending))
        {
            queue.RemoveFaceEdit(pending.Sequence);
            queue.RemoveSupersededFaceEdits(entity, in coordinate, faceMask, mode, message.Sequence);
            if (pending.Entity == entity &&
                pending.Coordinate == coordinate &&
                pending.FaceMask == faceMask &&
                pending.Mode == mode &&
                string.Equals(pending.PredictedPaintId, serverPaintId, System.StringComparison.Ordinal))
            {
                if (queue.HasNewerFaceEdit(entity, in coordinate, faceMask, mode, message.Sequence) ||
                    FacesMatchPaint(voxelComponent.VoxelObject, in coordinate, faceMask, mode, localPaintId))
                {
                    return;
                }
            }
            else
            {
                //  Revert the local prediction first so no stale predicted faces remain when snapping to authority.
                RestoreFaceOverrides(store, pending.Entity, in pending.Coordinate, pending.FaceMask, pending.OriginalOverrides);
            }
        }

        if (voxelComponent.VoxelObject.Get(coordinate.X, coordinate.Y, coordinate.Z).ID == 0)
        {
            return;
        }

        if (mode == PaintKind.Texture)
        {
            voxelComponent.VoxelObject.SetFaceTexture(coordinate.X, coordinate.Y, coordinate.Z, faceMask, localPaintId);
        }
        else
        {
            voxelComponent.VoxelObject.SetFaceColor(coordinate.X, coordinate.Y, coordinate.Z, faceMask, localPaintId);
        }

        voxelComponent.Registry.MarkFaceModified(coordinate.X, coordinate.Y, coordinate.Z, voxelComponent.VoxelObject.ChunkSize);
        store.MarkDirty<VoxelComponent>(entity);
    }

    private static bool FacesMatchPaint(VoxelObject voxelObject, in Int3 coordinate, BrickFaceMask faceMask, PaintKind mode, ushort expectedPaintId)
    {
        for (var f = 0; f < 6; f++)
        {
            var face = (BrickFace)f;
            if (!faceMask.HasFace(face))
            {
                continue;
            }

            voxelObject.TryGetFaceOverride(coordinate.X, coordinate.Y, coordinate.Z, face, out FaceOverride current);
            ushort actual = mode == PaintKind.Texture ? current.TexturePaintId : current.ColorPaintId;
            if (actual != expectedPaintId)
            {
                return false;
            }
        }

        return true;
    }

    private static void RestoreFaceOverrides(DataStore store, int entity, in Int3 coordinate, BrickFaceMask faceMask, FaceOverride[] originalOverrides)
    {
        if (!store.TryGet(entity, out VoxelComponent voxelComponent) ||
            voxelComponent.VoxelObject.Get(coordinate.X, coordinate.Y, coordinate.Z).ID == 0)
        {
            return;
        }

        for (var f = 0; f < 6 && f < originalOverrides.Length; f++)
        {
            var face = (BrickFace)f;
            if (faceMask.HasFace(face))
            {
                voxelComponent.VoxelObject.SetFaceOverride(coordinate.X, coordinate.Y, coordinate.Z, face.ToMask(), originalOverrides[f]);
            }
        }

        voxelComponent.Registry.MarkFaceModified(coordinate.X, coordinate.Y, coordinate.Z, voxelComponent.VoxelObject.ChunkSize);
        store.MarkDirty<VoxelComponent>(entity);
    }

    private void WriteVoxel(DataStore store, int entity, in Int3 coordinate, in Voxel voxel)
    {
        if (!store.TryGet(entity, out VoxelComponent voxelComponent))
        {
            return;
        }

        Voxel original = voxelComponent.VoxelObject.Get(coordinate.X, coordinate.Y, coordinate.Z);
        Voxel updated = voxel;
        if (original.ID == 0 && updated.ID == 0)
        {
            updated.ShapeLight = new ShapeLight(updated.GetShapeLight().Shape, original.GetLightLevel());
        }

        voxelComponent.VoxelObject.Set(coordinate.X, coordinate.Y, coordinate.Z, updated);
        _voxelLightingService.UpdateLighting(voxelComponent.VoxelObject, voxelComponent.Registry, coordinate.X, coordinate.Y, coordinate.Z, in original, ref updated);

        //  Publish the edit by marking the voxel component dirty; the VoxelEntityRebuildSystem observes
        //  this flag and fulfills the mesh/collider rebuild on the ECS thread.
        store.MarkDirty<VoxelComponent>(entity);
    }

    private bool VoxelEquals(in Voxel predicted, in Voxel server, string? serverBrickId)
    {
        if (predicted.GetShapeLight().Shape != server.GetShapeLight().Shape || predicted.GetOrientation() != server.GetOrientation())
        {
            return false;
        }

        //  Compare by canonical brick name so confirmation holds even when the client and server resolve
        //  different numeric ids for the same brick. Fall back to the raw id when the echo carries no name.
        if (!string.IsNullOrEmpty(serverBrickId))
        {
            return _brickRegistry.Name(predicted.ID) == serverBrickId;
        }

        return predicted.ID == server.ID;
    }
}