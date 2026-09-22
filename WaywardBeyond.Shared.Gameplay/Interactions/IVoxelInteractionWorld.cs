using System.Numerics;
using Swordfish.ECS;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Voxels;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// Abstraction over the caller's world (client prediction store or server authority store) that the
/// shared interaction targeting ports its raycasts against. Both sides build colliders from the same
/// shared voxel data, so raycasting through this abstraction yields the same hit on both sides and the
/// resolver picks the same cell from the same ray. Implemented per-world; a structure hit is resolved to
/// its mutable voxel container and transform so the resolver can read occupancy.
/// </summary>
public interface IVoxelInteractionWorld
{
    /// <summary>Raycasts the world's physics, returning the raw hit (entity, point, normal).</summary>
    bool TryRaycast(in Ray ray, out RaycastResult result);

    /// <summary>Reads a hit structure's live voxel container and world transform, if it has them.</summary>
    bool TryGetVoxelTarget(int entity, out VoxelObject? voxelObject, out TransformComponent transform);
}