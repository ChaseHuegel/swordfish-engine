using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Gameplay.Interactions;

namespace WaywardBeyond.Client.Systems;

/// <summary>
///     Client-side <see cref="IVoxelInteractionWorld"/> for prediction. Reads a structure's live voxel container
///     + transform from the client store by the structure's stable <see cref="Uuid"/> for ray-free validation.
/// </summary>
public sealed class ClientVoxelInteractionWorld(DataStore store) : IVoxelInteractionWorld
{
    public Result<VoxelTarget> GetTarget(in Uuid entityUuid)
    {
        if (store.TryGet(entityUuid, out int entity) &&
            store.TryGet(entity, out VoxelComponent voxelComponent) &&
            store.TryGet(entity, out TransformComponent transformComponent))
        {
            return Result<VoxelTarget>.FromSuccess(new VoxelTarget(entity, voxelComponent.VoxelObject, transformComponent));
        }

        return Result<VoxelTarget>.FromFailure((string?)null);
    }
}