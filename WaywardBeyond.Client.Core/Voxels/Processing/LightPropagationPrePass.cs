using WaywardBeyond.Client.Core.Bricks;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class LightPropagationPrePass(LightingState lightingState, IBrickDatabase brickDatabase) 
    : LightPrePass(brickDatabase), VoxelObjectProcessor.ISamplePass
{
    private readonly LightingState _lightingState = lightingState;
    
    public VoxelObjectProcessor.Stage Stage => VoxelObjectProcessor.Stage.PrePass;
    
    public void Process(VoxelSample sample)
    {
        if (sample.Center.GetLightLevel() == 0)
        {
            return;
        }
        
        int x = sample.Coords.X + sample.ChunkOffset.X;
        int y = sample.Coords.Y + sample.ChunkOffset.Y;
        int z = sample.Coords.Z + sample.ChunkOffset.Z;
        var light = new LightingState.VoxelLight(x, y, z, sample.Center);
        _lightingState.ToPropagate.Enqueue(light);
    }
}