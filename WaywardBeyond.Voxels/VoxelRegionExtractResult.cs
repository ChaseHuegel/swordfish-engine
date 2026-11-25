using WaywardBeyond.Data;

namespace WaywardBeyond.Voxels;

/// <summary>Extracted voxel region together with the count of non-empty bricks inside it.</summary>
public readonly record struct VoxelRegionExtractResult(VoxelRegionData Region, int BrickCount);
