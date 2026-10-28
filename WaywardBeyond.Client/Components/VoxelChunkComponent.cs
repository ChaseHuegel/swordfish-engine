using Swordfish.ECS;
using Swordfish.Graphics;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Components;

/// <summary>
/// Identifies a chunk child entity belonging to a parent voxel structure.
/// </summary>
public readonly struct VoxelChunkComponent(Short3 chunkOffset, AlphaMode alphaMode = AlphaMode.Opaque) : IDataComponent
{
    public readonly Short3 ChunkOffset = chunkOffset;
    public readonly AlphaMode AlphaMode = alphaMode;
    public bool IsTransparent => AlphaMode == AlphaMode.Blend;
}
