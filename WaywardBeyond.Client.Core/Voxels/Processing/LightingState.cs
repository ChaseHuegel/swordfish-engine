using System.Collections.Generic;
using Swordfish.ECS;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class LightingState
{
    public readonly Queue<VoxelLight> ToPropagate = [];
    
    public readonly List<LightSource> Sources = [];
    
    public record struct VoxelLight(int X, int Y, int Z, Voxel Voxel);
    public record struct LightSource(int X, int Y, int Z, LightComponent light);
}