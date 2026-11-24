using Microsoft.Extensions.Logging;
using Krill;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Server.Interactions;

/// <summary>
/// Commit stage handler for <see cref="InteractionAction.Paint"/>. Updates the active mode slot on the live
/// <see cref="VoxelObject"/> without rebuilding physics colliders, then broadcasts <see cref="VoxelFaceEditMessage"/>.
/// </summary>
public sealed class VoxelFaceWriteCommitter(
    ServerPeers peers,
    ILogger<VoxelFaceWriteCommitter> logger
) : IInteractionHandler
{
    /// <inheritdoc/>
    public InteractionStage Stage => InteractionStage.Commit;

    /// <inheritdoc/>
    public InteractionPriority Priority => InteractionPriority.Normal;

    /// <inheritdoc/>
    public InteractionHandlerFilter Filter => default;

    /// <inheritdoc/>
    public InteractionDecision Handle(ref InteractionContext context)
    {
        InteractionResolution resolution = context.Resolution;
        if (resolution.Action != InteractionAction.Paint)
        {
            return InteractionDecision.Continue;
        }

        if (!context.Store.TryGet(resolution.Entity, out VoxelWorldComponent voxelWorldComponent))
        {
            logger.LogWarning(
                "Resolved paint interaction on entity {entity} but it no longer has a live voxel container.",
                resolution.Entity);
            return InteractionDecision.Cancel;
        }

        VoxelObject voxelObject = voxelWorldComponent.VoxelObject;
        Int3 coordinate = resolution.Coordinate;
        PaintResolution paint = resolution.Paint;

        if (paint.Mode == PaintKind.Texture)
        {
            voxelObject.SetFaceTexture(coordinate.X, coordinate.Y, coordinate.Z, paint.FaceMask, paint.PaintId);
        }
        else
        {
            voxelObject.SetFaceColor(coordinate.X, coordinate.Y, coordinate.Z, paint.FaceMask, paint.PaintId);
        }

        Broadcast(context.Store, resolution.Entity, coordinate, in paint, context.ClientId, context.Sequence);

        logger.LogDebug(
            "Applied face paint ({mode}, mask={mask}, paint={paint}) on entity {entity} at {coordinate} for player {player}.",
            paint.Mode, paint.FaceMask, paint.CanonicalPaintId ?? "clear", resolution.Entity, coordinate, context.Player);
        return InteractionDecision.Handled;
    }

    private void Broadcast(DataStore store, int entity, Int3 coordinate, in PaintResolution paint, Uuid originClientId, uint sequence)
    {
        ulong entityUuid = store.GetUuid(entity).ToValue();
        foreach (ServerPeer peer in peers.Peers)
        {
            if (peer.World?.Store != store)
            {
                continue;
            }

            //  Echo the interaction sequence only to the acting client; witness peers receive 0 so another
            //  player's sequence never collides with their own in-flight prediction queue.
            uint peerSequence = originClientId != Uuid.Null && peer.GameId == originClientId ? sequence : 0u;
            var message = new VoxelFaceEditMessage
            {
                EntityUuid = entityUuid,
                X = coordinate.X,
                Y = coordinate.Y,
                Z = coordinate.Z,
                FaceMask = (byte)paint.FaceMask,
                Mode = (byte)paint.Mode,
                PaintId = paint.CanonicalPaintId,
                Sequence = peerSequence,
            };

            if (!peer.Client.VoxelFaceEdit(message))
            {
                peer.CloseOnRejectedSend(logger);
            }
        }
    }
}
