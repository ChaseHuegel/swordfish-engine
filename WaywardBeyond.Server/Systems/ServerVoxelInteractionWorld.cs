using Krill;
using Swordfish.ECS;
using Swordfish.Library.Util;
using Swordfish.Physics;
using WaywardBeyond.Bricks;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Server-side <see cref="IVoxelInteractionWorld"/>: reads a structure's live voxel container + transform
/// from the authority store by its stable identity and checks placement overlap against the world physics.
/// </summary>
public sealed class ServerVoxelInteractionWorld(DataStore store, IPhysics physics) : IVoxelInteractionWorld
{
    /// <inheritdoc/>
    public Result<VoxelTarget> GetTarget(in Uuid entityUuid)
    {
        if (store.TryGet(entityUuid, out int entity) &&
            store.TryGet(entity, out VoxelWorldComponent world) &&
            store.TryGet(entity, out TransformComponent transformComponent))
        {
            return Result<VoxelTarget>.FromSuccess(new VoxelTarget(entity, world.VoxelObject, transformComponent));
        }

        return Result<VoxelTarget>.FromFailure((string?)null);
    }

    /// <inheritdoc/>
    public bool IntersectsCollider(in VoxelTarget target, in Int3 coordinate, Brick placeable, BrickShape shape, Orientation orientation)
    {
        return InteractionResolver.IntersectsCollider(physics, target.Entity, in target.Transform, in coordinate, placeable, shape, orientation);
    }
}