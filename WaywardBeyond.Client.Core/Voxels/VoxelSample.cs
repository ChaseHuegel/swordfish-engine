using WaywardBeyond.Client.Core.Numerics;

namespace WaywardBeyond.Client.Core.Voxels;

public ref struct VoxelSample(
    Int3 chunkCoords,
    Int3 coords,
    Voxel center,
    Voxel left,
    Voxel right,
    Voxel ahead,
    Voxel behind,
    Voxel above,
    Voxel below
) {
    public Int3 ChunkCoords = chunkCoords;
    public Int3 Coords = coords;
    public Voxel Center = center;
    public Voxel Left = left;
    public Voxel Right = right;
    public Voxel Ahead = ahead;
    public Voxel Behind = behind;
    public Voxel Above = above;
    public Voxel Below = below;
}