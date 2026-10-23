using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>The resolved outcome of an interaction.</summary>
/// <remarks><see cref="InteractionAction.None"/> is a valid, expected outcome.</remarks>
public readonly struct InteractionResolution(InteractionAction action, int entity, Int3 coordinate, Voxel voxel)
{
    public readonly InteractionAction Action = action;
    public readonly int Entity = entity;
    public readonly Int3 Coordinate = coordinate;
    public readonly Voxel Voxel = voxel;

    public static readonly InteractionResolution None = new(action: InteractionAction.None, entity: 0, coordinate: default, voxel: default);
}