using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Processing;

internal sealed class LightSeedPrePass(IBrickDatabase brickDatabase) : VoxelObjectProcessor.IVoxelPass
{
    public VoxelObjectProcessor.Stage Stage => VoxelObjectProcessor.Stage.PrePass;

    public bool ShouldProcessChunk(ChunkData chunkData)
    {
        return true;
    }

    public void Process(ref Voxel voxel)
    {
        Result<Brick> brickInfoResult = brickDatabase.Get(voxel.ID);
        
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