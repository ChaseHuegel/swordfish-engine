using Swordfish.ECS;
using WaywardBeyond.Client.Voxels;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Components;

/// <summary>
/// Associates a voxel structure container and chunk renderer registry with an entity.
/// </summary>
public readonly struct VoxelComponent(in VoxelObject voxelObject, in VoxelChunkRegistry? registry = null) : IDataComponent
{
    public readonly VoxelObject VoxelObject = voxelObject;
    public readonly VoxelChunkRegistry Registry = registry ?? new VoxelChunkRegistry();
}
