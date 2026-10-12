using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>
///     Abstraction over the caller's world (client prediction store or server authority store) that the
///     shared interaction validation reads structure occupancy and position from. Validation never raycasts -
///     the hint was produced by the client's screen-aim targeting.
/// </summary>
public interface IVoxelInteractionWorld
{
    /// <summary>Reads a structure by its stable identity into its live voxel container and world transform.</summary>
    Result<VoxelTarget> GetTarget(in Uuid entityUuid);
}

/// <summary>The target facts validation reads: the structure entity, its live voxel container, and its transform.</summary>
public readonly struct VoxelTarget(int entity, VoxelObject? voxelObject, TransformComponent transform)
{
    public readonly int Entity = entity;
    public readonly VoxelObject? VoxelObject = voxelObject;
    public readonly TransformComponent Transform = transform;
}