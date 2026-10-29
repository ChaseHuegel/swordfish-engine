using WaywardBeyond.Bricks;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Processing;

internal sealed class DepthPrePass(DepthState depthState, IBrickDatabase brickDatabase) : VoxelObjectProcessor.ISamplePass
{
    public VoxelObjectProcessor.Stage Stage => VoxelObjectProcessor.Stage.PrePass;

    public bool ShouldProcessChunk(ChunkData chunkData)
    {
        return !chunkData.Palette.Only(id: 0);
    }

    public void Process(VoxelSample sample)
    {
        if (sample.Center.ID == 0 || !brickDatabase.IsCuller(sample.Center, sample.Center.GetShapeLight().Shape))
        {
            return;
        }
        
        int x = sample.Coords.X + sample.ChunkOffset.X;
        int y = sample.Coords.Y + sample.ChunkOffset.Y;
        int z = sample.Coords.Z + sample.ChunkOffset.Z;
        
        depthState.Add(x, y, z);
    }
}
