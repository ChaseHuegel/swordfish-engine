using WaywardBeyond.Data;

namespace WaywardBeyond.Voxels;

public readonly ref struct ChunkVoxel(ChunkData chunkData, ref Voxel voxel, int index)
{
    internal readonly ChunkData ChunkData = chunkData;
    internal readonly ref Voxel Voxel = ref voxel;
    internal readonly int Index = index;
}
