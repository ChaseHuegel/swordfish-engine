using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class VoxelEntityPostPass(IBrickDatabase brickDatabase, EntityState entityState) : VoxelObjectProcessor.ISamplePass
{
    private readonly IBrickDatabase _brickDatabase = brickDatabase;
    private readonly EntityState _entityState = entityState;
    
    public VoxelObjectProcessor.Stage Stage => VoxelObjectProcessor.Stage.PostPass;

    public bool ShouldProcessChunk(ChunkData chunkData)
    {
        return true;
    }

    public void Process(VoxelSample sample)
    {
        Result<BrickInfo> brickInfoResult = _brickDatabase.Get(sample.Center.ID);
        BrickInfo brickInfo = brickInfoResult.Value;
        if (!brickInfoResult.Success || !brickInfo.Entity)
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