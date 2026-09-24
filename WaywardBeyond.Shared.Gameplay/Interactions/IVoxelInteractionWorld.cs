using System.Numerics;
using Swordfish.ECS;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Voxels;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// Abstraction over the caller's world (client prediction store or server authority store) that the
/// shared interaction validation reads structure occupancy and position from. A structure is addressed by
/// its stable <see cref="Uuid"/> (the identity carried on the wire in the target hint), and resolved to its
/// mutable voxel container + transform so the resolver can validate reach and occupancy. The ray-based
/// <see cref="TryRaycast"/>/int-lookup pair remains only for the client's screen-aim targeting; server
/// validation never raycasts.
/// </summary>
public interface IVoxelInteractionWorld
{
    /// <summary>Raycasts the world's physics. Retained for client aim targeting; not used by validation.</summary>
    bool TryRaycast(in Ray ray, out RaycastResult result);

    /// <summary>Reads a raycast-hit structure's live voxel container and world transform, if it has them.</summary>
    bool TryGetVoxelTarget(int entity, out VoxelObject? voxelObject, out TransformComponent transform);

    /// <summary>Reads a structure by its stable identity into its live voxel container and world transform.</summary>
    bool TryGetVoxelTarget(in Uuid entityUuid, out int entity, out VoxelObject? voxelObject, out TransformComponent transform);
}