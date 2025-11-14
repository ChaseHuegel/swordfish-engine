using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class LightSeedPrePass(in IBrickDatabase brickDatabase) 
    : LightPrePass(brickDatabase), VoxelObjectProcessor.IVoxelPass
{
    public VoxelObjectProcessor.Stage Stage => VoxelObjectProcessor.Stage.PrePass;
    
    public void Process(ref Voxel voxel)
    {
        Result<BrickInfo> brickInfoResult = BrickDatabase.Get(voxel.ID);
        
        ShapeLight shapeLight = voxel.ShapeLight;
        BrickInfo brickInfo = brickInfoResult.Value;
        
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