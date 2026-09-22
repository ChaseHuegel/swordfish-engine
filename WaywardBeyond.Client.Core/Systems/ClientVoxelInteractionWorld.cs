using Swordfish.ECS;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Shared.Gameplay;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Client-side <see cref="IVoxelInteractionWorld"/> for prediction. Raycasts the client's physics world
/// (whose structure colliders are built from the same shared <see cref="VoxelColliderBuilder"/> as the
/// server's) and reads a hit structure's live voxel container + transform from the client store, so the
/// shared interaction targeting derives the same cell from the same ray on both sides.
/// </summary>
public sealed class ClientVoxelInteractionWorld(DataStore store, IPhysics physics) : IVoxelInteractionWorld
{
    public bool TryRaycast(in Ray ray, out RaycastResult result)
    {
        result = physics.Raycast(ray);
        return result.Hit;
    }

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
}