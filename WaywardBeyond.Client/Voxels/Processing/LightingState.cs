using System.Collections.Generic;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.Voxels.Processing;

public sealed class LightingState
{
    public readonly Queue<VoxelLight> ToPropagate = [];
    
    public record struct VoxelLight(int X, int Y, int Z, Voxel Voxel);
}