using WaywardBeyond.Client.Core.Numerics;

namespace WaywardBeyond.Client.Core.Voxels;

public ref struct VoxelSample
{
    public Int3 ChunkOffset;
    public Int3 ChunkCoords;
    public Int3 Coords;
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
        ref Voxel below
    ) {
        ChunkOffset = chunkOffset;
        ChunkCoords = chunkCoords;
        Coords = coords;
        Center = ref center;
        Left = ref left;
        Right = ref right;
        Ahead = ref ahead;
        Behind = ref behind;
        Above = ref above;
        Below = ref below;
    }
}