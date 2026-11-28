using Krill;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Gameplay.Components;

/// <summary>Anchors a brick-entity to a specific voxel coordinate on a voxel structure entity.</summary>
public readonly record struct BrickAnchorComponent(Uuid VoxelEntityUuid, Int3 VoxelCoord) : IDataComponent;
