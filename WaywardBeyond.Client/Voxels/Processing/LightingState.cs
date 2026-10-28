using System.Collections.Generic;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.Voxels.Processing;

/// <summary>
/// Holds the queue of voxels awaiting light propagation during full builds.
/// </summary>
public sealed class LightingState
{
    public readonly Queue<VoxelLight> ToPropagate = [];
    
    /// <summary>Represents a positioned voxel entry for light propagation.</summary>
    public record struct VoxelLight(int X, int Y, int Z, Voxel Voxel);
}