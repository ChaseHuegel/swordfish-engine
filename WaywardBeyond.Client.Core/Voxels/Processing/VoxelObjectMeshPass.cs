namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class VoxelObjectMeshPass : VoxelObjectProcessor.ISamplePass
{
    public VoxelObjectProcessor.Stage Stage => VoxelObjectProcessor.Stage.PostPass;
    
    public void Process(VoxelSample sample)
    {
    }
}