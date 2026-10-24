using Swordfish.ECS;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>The target facts validation reads: the structure entity, its live voxel container, and its transform.</summary>
public readonly struct VoxelTarget(int entity, VoxelObject? voxelObject, TransformComponent transform)
{
    public readonly int Entity = entity;
    public readonly VoxelObject? VoxelObject = voxelObject;
    public readonly TransformComponent Transform = transform;
}