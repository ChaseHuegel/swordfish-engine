using Swordfish.ECS;
using Swordfish.Library.Util;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>
///     Abstraction over the caller's world (client prediction store or server authority store) that the
///     shared interaction validation reads structure occupancy and position from.
///     Validation never raycasts. The hint is produced by the client's screen-aim targeting.
/// </summary>
public interface IVoxelInteractionWorld
{
    /// <summary>Reads a structure by its stable identity into its live voxel container and world transform.</summary>
    Result<VoxelTarget> GetTarget(in Uuid entityUuid);
}