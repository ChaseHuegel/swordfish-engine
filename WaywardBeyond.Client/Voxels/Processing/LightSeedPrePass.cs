using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Processing;

/// <summary>
/// Sets initial light levels for light-emitting bricks during full builds.
/// </summary>
internal sealed class LightSeedPrePass(IBrickDatabase brickDatabase) : VoxelObjectProcessor.IVoxelPass
{
    /// <inheritdoc/>
    public VoxelObjectProcessor.Stage Stage => VoxelObjectProcessor.Stage.PrePass;

    /// <inheritdoc/>
    public bool ShouldProcessChunk(ChunkData chunkData)
    {
        return true;
    }

    /// <inheritdoc/>
    public void Process(ref Voxel voxel)
    {
        ShapeLight shapeLight = voxel.GetShapeLight();
        if (voxel.ID == 0)
        {
            voxel.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 0);
            return;
        }

        Result<Brick> brickInfoResult = brickDatabase.Get(voxel.ID);
        if (!brickInfoResult.Success || !brickInfoResult.Value.LightSource)
        {
            voxel.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 0);
            return;
        }

        voxel.ShapeLight = new ShapeLight(shapeLight.Shape, brickInfoResult.Value.Brightness);
    }
}