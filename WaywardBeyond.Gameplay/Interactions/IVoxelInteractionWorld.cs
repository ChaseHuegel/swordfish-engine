using Swordfish.ECS;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>
/// Abstraction over the caller's world (client prediction store or server authority store) that the
/// shared interaction validation reads structure occupancy and position from. A structure is addressed by
/// its stable <see cref="Uuid"/> (the identity carried on the wire in the target hint), and resolved to its
/// mutable voxel container + transform so the resolver can validate reach and occupancy. Validation never
/// raycasts - the client's screen-aim targeting produced the hint.
/// </summary>
public interface IVoxelInteractionWorld
{
    /// <summary>Reads a structure by its stable identity into its live voxel container and world transform.</summary>
    bool TryGetVoxelTarget(in Uuid entityUuid, out int entity, out VoxelObject? voxelObject, out TransformComponent transform);
}