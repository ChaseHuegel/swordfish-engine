using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Voxels.Models;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.Voxels.Processing;

internal sealed class LightSeedPrePass(IBrickDatabase brickDatabase) : VoxelObjectProcessor.IVoxelPass
{
    private readonly IBrickDatabase _brickDatabase = brickDatabase;
    
    public VoxelObjectProcessor.Stage Stage => VoxelObjectProcessor.Stage.PrePass;

    public bool ShouldProcessChunk(ChunkData chunkData)
    {
        return true;
    }

    public void Process(ref Voxel voxel)
    {
        Result<Brick> brickInfoResult = _brickDatabase.Get(voxel.ID);
        
        ShapeLight shapeLight = voxel.ShapeLight;
        Brick brickInfo = brickInfoResult.Value;
        
        // If this isn't a light, clear any stale light level
        if (!brickInfoResult.Success || !brickInfo.LightSource)
        {
            voxel.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 0);
            return;
        }
        
        //  Otherwise, seed the light
        voxel.ShapeLight = new ShapeLight(shapeLight.Shape, brickInfo.Brightness);
    }
}