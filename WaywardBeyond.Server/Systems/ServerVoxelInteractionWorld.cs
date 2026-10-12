using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Server.Components;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Server-side <see cref="IVoxelInteractionWorld"/>: reads a structure's live voxel container + transform
/// from the authority store by its stable identity.
/// </summary>
public sealed class ServerVoxelInteractionWorld(DataStore store) : IVoxelInteractionWorld
{
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
}