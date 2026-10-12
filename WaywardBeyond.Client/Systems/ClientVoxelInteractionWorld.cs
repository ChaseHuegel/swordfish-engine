using Swordfish.ECS;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Voxels;
using WaywardBeyond.Gameplay.Interactions;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// Client-side <see cref="IVoxelInteractionWorld"/> for prediction. Reads a structure's live voxel container
/// + transform from the client store by the structure's stable <see cref="Uuid"/> for ray-free validation.
/// </summary>
public sealed class ClientVoxelInteractionWorld(DataStore store) : IVoxelInteractionWorld
{
    public bool TryGetVoxelTarget(in Uuid entityUuid, out int entity, out VoxelObject? voxelObject, out TransformComponent transform)
    {
        if (store.TryGet(entityUuid, out entity) && store.TryGet(entity, out VoxelComponent voxelComponent) && store.TryGet(entity, out TransformComponent transformComponent))
        {
            voxelObject = voxelComponent.VoxelObject;
            transform = transformComponent;
            return true;
        }

        entity = default;
        voxelObject = null;
        transform = default;
        return false;
    }
}