using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>
///     The facts handed to an interaction handler after base validation: the original request, the base
///     resolution, the target entity, the target voxel, and the held item.
/// </summary>
public readonly struct InteractionContext(
    in InteractionRequest request,
    in InteractionResolution resolution,
    string? heldItemID)
{
    /// <summary>The requested interaction.</summary>
    public readonly InteractionRequest Request = request;

    /// <summary>The base-resolved outcome the shared resolver produced before handlers ran.</summary>
    public readonly InteractionResolution Resolution = resolution;

    /// <summary>The target entity the resolved action applies to.</summary>
    public readonly int Entity = resolution.Entity;

    /// <summary>The target coordinate in the entity's voxel space.</summary>
    public readonly Int3 Coordinate = resolution.Coordinate;

    /// <summary>The voxel at the target: the removed voxel for a break, the authored voxel for a place.</summary>
    public readonly Voxel CurrentVoxel = resolution.Voxel;

    /// <summary>The id of the item currently held by the interacting player, or null.</summary>
    public readonly string? HeldItemID = heldItemID;
}