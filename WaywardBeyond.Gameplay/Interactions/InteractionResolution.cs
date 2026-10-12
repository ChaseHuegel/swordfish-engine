using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>
///     The resolved outcome of an interaction: the action, the target cell, and the voxel involved (removed
///     for a break, authored for a place). A <see cref="InteractionAction.None"/> resolution is a valid,
///     expected outcome - it never indicates an error.
/// </summary>
public readonly struct InteractionResolution(InteractionAction action, int entity, Int3 coordinate, Voxel voxel)
{
    public readonly InteractionAction Action = action;
    public readonly int Entity = entity;
    public readonly Int3 Coordinate = coordinate;
    public readonly Voxel Voxel = voxel;

    public static readonly InteractionResolution None = new(InteractionAction.None, default, default, default);
}