using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Client.Core.Voxels.Models;

public record struct VoxelInfo(int X, int Y, int Z, Voxel Voxel);