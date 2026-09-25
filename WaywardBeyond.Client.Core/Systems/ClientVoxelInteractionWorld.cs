using Swordfish.ECS;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Shared.Gameplay;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Client-side <see cref="IVoxelInteractionWorld"/> for prediction. Reads a structure's live voxel container
/// + transform from the client store - by entity for int-lookup, or by the structure's stable <see cref="Uuid"/>
/// for ray-free validation.
/// </summary>
public sealed class ClientVoxelInteractionWorld(DataStore store) : IVoxelInteractionWorld
{
    public bool TryGetVoxelTarget(int entity, out VoxelObject? voxelObject, out TransformComponent transform)
    {
        if (store.TryGet(entity, out VoxelComponent voxelComponent) && store.TryGet(entity, out TransformComponent transformComponent))
        {
            voxelObject = voxelComponent.VoxelObject;
            transform = transformComponent;
            return true;
        }

        voxelObject = null;
        transform = default;
        return false;
    }

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