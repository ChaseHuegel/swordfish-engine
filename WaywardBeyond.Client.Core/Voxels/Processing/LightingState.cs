using System.Collections.Generic;
using Swordfish.ECS;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

public sealed class LightingState
{
    public readonly Queue<VoxelLight> ToPropagate = [];
    
    public record struct VoxelLight(int X, int Y, int Z, Voxel Voxel);
}