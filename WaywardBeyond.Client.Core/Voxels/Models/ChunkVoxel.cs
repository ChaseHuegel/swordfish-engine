namespace WaywardBeyond.Client.Core.Voxels.Models;

public readonly ref struct ChunkVoxel(ChunkData chunkData, ref Voxel voxel)
{
    internal readonly ChunkData ChunkData = chunkData;
    internal readonly ref Voxel Voxel = ref voxel;
}