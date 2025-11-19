using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class LightSourcePostPass(in IBrickDatabase brickDatabase, in EntityState entityState) 
    : LightPrePass(brickDatabase), VoxelObjectProcessor.ISamplePass
{
    private readonly EntityState _entityState = entityState;
    
    public VoxelObjectProcessor.Stage Stage => VoxelObjectProcessor.Stage.PostPass;
    
    public void Process(VoxelSample sample)
    {
        Result<BrickInfo> brickInfoResult = BrickDatabase.Get(sample.Center.ID);
        BrickInfo brickInfo = brickInfoResult.Value;
        if (!brickInfoResult.Success || !brickInfo.LightSource)
        {
            return;
        }
        
        int x = sample.Coords.X + sample.ChunkOffset.X;
        int y = sample.Coords.Y + sample.ChunkOffset.Y;
        int z = sample.Coords.Z + sample.ChunkOffset.Z;
        var voxelInfo = new VoxelInfo(x, y, z, sample.Center);
        _entityState.Voxels.Add(voxelInfo);
    }
}