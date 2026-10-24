using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Server.Interactions;

/// <summary>
/// Apply stage, low priority: the default authority write. Writes the resolved voxel, rebuilds the
/// structure collider, and broadcasts the edit. A handler that registers earlier and returns
/// <see cref="InteractionDecision.Handled"/> replaces it.
/// </summary>
public sealed class VoxelWriteHandler(
    ServerPeers peers,
    IBrickRegistry brickRegistry,
    ILogger<VoxelWriteHandler> logger
) : IInteractionHandler
{
    /// <inheritdoc/>
    public InteractionStage Stage => InteractionStage.Apply;

    /// <inheritdoc/>
    public InteractionPriority Priority => InteractionPriority.Low;

    /// <inheritdoc/>
    public InteractionHandlerFilter Filter => default;

    /// <inheritdoc/>
    public InteractionDecision Handle(ref InteractionContext context)
    {
        InteractionResolution resolution = context.Resolution;
        if (resolution.Action == InteractionAction.None)
        {
            return InteractionDecision.Continue;
        }

        if (!context.Store.TryGet(resolution.Entity, out VoxelWorldComponent voxelWorldComponent))
        {
            logger.LogWarning(
                "Resolved interaction on entity {entity} but it no longer has a live voxel container.",
                resolution.Entity);
            return InteractionDecision.Cancel;
        }

        VoxelObject voxelObject = voxelWorldComponent.VoxelObject;
        Int3 coordinate = resolution.Coordinate;
        Voxel voxel = resolution.Action == InteractionAction.Break ? new Voxel() : resolution.Voxel;

        voxelObject.Set(coordinate.X, coordinate.Y, coordinate.Z, voxel);

        //  Rebuild the structure's collider so subsequent authority raycasts see the change. The live
        //  VoxelObject is the single voxel representation; saves and streams re-derive chunks from it.
        context.Store.AddOrUpdate(
            resolution.Entity,
            new ColliderComponent(VoxelColliderBuilder.BuildCollision(voxelObject.GetChunkInfos())));

        Voxel written = voxelObject.Get(coordinate.X, coordinate.Y, coordinate.Z);
        Broadcast(context.Store, resolution.Entity, coordinate, in written, context.Sequence);

        logger.LogDebug(
            "Applied {action} on entity {entity} at {coordinate} for player {player}.",
            resolution.Action, resolution.Entity, coordinate, context.Player);
        return InteractionDecision.Handled;
    }

    /// <summary>
    /// Sends the authoritative edit to every client in the world so both the origin client and remote
    /// witnesses apply the same delta. The sequence lets the origin client resolve the prediction it echoed.
    /// </summary>
    private void Broadcast(DataStore store, int entity, Int3 coordinate, in Voxel voxel, uint sequence)
    {
        var message = new VoxelEditMessage
        {
            EntityUuid = store.GetUuid(entity).ToValue(),
            X = coordinate.X,
            Y = coordinate.Y,
            Z = coordinate.Z,
            Voxel = voxel,
            Sequence = sequence,
            BrickId = brickRegistry.Name(voxel.ID),
        };

        foreach (ServerPeer peer in peers.Peers)
        {
            if (peer.World?.Store != store)
            {
                continue;
            }

            if (!peer.Rpc.Notify(GameRpc.Methods.VOXEL_EDIT, in message))
            {
                peer.CloseOnRejectedSend(logger);
            }
        }
    }
}
