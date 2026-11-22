using WaywardBeyond.Data;

namespace WaywardBeyond.Voxels;

public ref struct VoxelSample
{
    public readonly Int3 ChunkOffset;
    public readonly Int3 ChunkCoords;
    public readonly Int3 Coords;
    public readonly ChunkData ChunkData;
    public readonly VoxelObject? VoxelObject;
    public readonly int VoxelIndex;
    public ref Voxel Center;
    public ref Voxel Left;
    public ref Voxel Right;
    public ref Voxel Ahead;
    public ref Voxel Behind;
    public ref Voxel Above;
    public ref Voxel Below;

    public VoxelSample(
        Int3 chunkOffset,
        Int3 chunkCoords,
        Int3 coords,
        ref Voxel center,
        ref Voxel left,
        ref Voxel right,
        ref Voxel ahead,
        ref Voxel behind,
        ref Voxel above,
        ref Voxel below,
        ChunkData chunkData = default,
        VoxelObject? voxelObject = null,
        int voxelIndex = -1
    ) {
        ChunkOffset = chunkOffset;
        ChunkCoords = chunkCoords;
        Coords = coords;
        ChunkData = chunkData;
        VoxelObject = voxelObject;
        VoxelIndex = voxelIndex;
        Center = ref center;
        Left = ref left;
        Right = ref right;
        Ahead = ref ahead;
        Behind = ref behind;
        Above = ref above;
        Below = ref below;
    }
}
