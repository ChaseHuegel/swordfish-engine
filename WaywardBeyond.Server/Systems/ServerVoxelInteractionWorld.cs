using Swordfish.ECS;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Server-side <see cref="IVoxelInteractionWorld"/>: reads a structure's live voxel container + transform
/// from the authority store by its stable identity. Validation never raycasts - the client's screen-aim
/// targeting produced the hint.
/// </summary>
public sealed class ServerVoxelInteractionWorld(DataStore store) : IVoxelInteractionWorld
{
    public bool TryGetVoxelTarget(in Uuid entityUuid, out int entity, out VoxelObject? voxelObject, out TransformComponent transform)
    {
        if (store.TryGet(entityUuid, out entity) &&
            store.TryGet(entity, out VoxelWorldComponent world) &&
            store.TryGet(entity, out TransformComponent transformComponent))
        {
            voxelObject = world.VoxelObject;
            transform = transformComponent;
            return true;
        }

        entity = default;
        voxelObject = null;
        transform = default;
        return false;
    }
}