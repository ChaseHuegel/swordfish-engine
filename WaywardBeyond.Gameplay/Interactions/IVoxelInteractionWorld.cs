using Krill;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>
///     Abstraction over the caller's world (client prediction store or server authority store) that the
///     shared interaction validation reads structure occupancy, position, and collider overlap from.
///     Validation never raycasts. The hint is produced by the client's screen-aim targeting.
/// </summary>
public interface IVoxelInteractionWorld
{
    /// <summary>Reads a structure by its stable identity into its live voxel container and world transform.</summary>
    Result<VoxelTarget> GetTarget(in Uuid entityUuid);

    /// <summary>Returns whether placing the brick at the target cell intersects another physics collider.</summary>
    bool IntersectsCollider(in VoxelTarget target, in Int3 coordinate, Brick placeable, BrickShape shape, Orientation orientation);
}