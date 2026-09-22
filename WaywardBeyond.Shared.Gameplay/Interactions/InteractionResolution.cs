using System.Numerics;
using Swordfish.ECS;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// The resolved outcome of a player interaction: the action, the target cell in the structure's brick
/// space, and (for a break) the voxel being removed or (for a place) the voxel to write. A resolution with
/// <see cref="InteractionAction.None"/> is a valid, expected outcome - it never indicates an error.
/// </summary>
public readonly struct InteractionResolution
{
    public readonly InteractionAction Action;
    public readonly int Entity;
    public readonly Int3 Coordinate;
    public readonly Voxel Voxel;

    public static readonly InteractionResolution None = new(InteractionAction.None, default, default, default);

    public InteractionResolution(InteractionAction action, int entity, Int3 coordinate, Voxel voxel)
    {
        Action = action;
        Entity = entity;
        Coordinate = coordinate;
        Voxel = voxel;
    }
}