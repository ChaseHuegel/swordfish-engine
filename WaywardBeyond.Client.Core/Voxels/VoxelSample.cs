namespace WaywardBeyond.Client.Core.Voxels;

public ref struct VoxelSample(
    Voxel center,
    Voxel left,
    Voxel right,
    Voxel ahead,
    Voxel behind,
    Voxel above,
    Voxel below
) {
    public Voxel Center = center;
    public Voxel Left = left;
    public Voxel Right = right;
    public Voxel Ahead = ahead;
    public Voxel Behind = behind;
    public Voxel Above = above;
    public Voxel Below = below;
}