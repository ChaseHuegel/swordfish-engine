using System.Collections.Generic;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class LightingState
{
    public readonly Queue<VoxelLight> Lights = [];

    public record struct VoxelLight(int X, int Y, int Z, Voxel Voxel);
}