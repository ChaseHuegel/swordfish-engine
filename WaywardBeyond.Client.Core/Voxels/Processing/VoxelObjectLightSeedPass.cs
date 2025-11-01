namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class VoxelObjectLightSeedPass : VoxelObjectProcessor.IVoxelPass
{
    public VoxelObjectProcessor.Stage Stage => VoxelObjectProcessor.Stage.PrePass;
    
    public void Process(ref Voxel voxel)
    {
    }
}