using System.Collections.Generic;
using WaywardBeyond.Client.Numerics;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Processing;

internal sealed class AmbientLightPass(LightingState lightingState, DepthState depthState) : VoxelObjectProcessor.IPass
{
    public void Process(VoxelObject voxelObject)
    {
        // Seed ambient light at resident min and max voxels at each depth plane.
        foreach (KeyValuePair<Int2, Int2> pair in depthState.XY)
        {
            Int2 coords = pair.Key;
            Int2 depth = pair.Value;
            
            VoxelSample min = voxelObject.Sample(coords.X, coords.Y, depth.Min);
            SeedAmbient(voxelObject, coords.X, coords.Y, depth.Min - 1, ref min.Behind);

            VoxelSample max = voxelObject.Sample(coords.X, coords.Y, depth.Max);
            SeedAmbient(voxelObject, coords.X, coords.Y, depth.Max + 1, ref max.Ahead);
        }
        
        foreach (KeyValuePair<Int2, Int2> pair in depthState.XZ)
        {
            Int2 coords = pair.Key;
            Int2 depth = pair.Value;
            
            VoxelSample min = voxelObject.Sample(coords.X, depth.Min, coords.Y);
            SeedAmbient(voxelObject, coords.X, depth.Min - 1, coords.Y, ref min.Below);

            VoxelSample max = voxelObject.Sample(coords.X, depth.Max, coords.Y);
            SeedAmbient(voxelObject, coords.X, depth.Max + 1, coords.Y, ref max.Above);
        }
        
        foreach (KeyValuePair<Int2, Int2> pair in depthState.ZY)
        {
            Int2 coords = pair.Key;
            Int2 depth = pair.Value;
            
            VoxelSample min = voxelObject.Sample(depth.Min, coords.Y, coords.X);
            SeedAmbient(voxelObject, depth.Min - 1, coords.Y, coords.X, ref min.Left);

            VoxelSample max = voxelObject.Sample(depth.Max, coords.Y, coords.X);
            SeedAmbient(voxelObject, depth.Max + 1, coords.Y, coords.X, ref max.Right);
        }
    }

    private void SeedAmbient(VoxelObject voxelObject, int x, int y, int z, ref Voxel voxel)
    {
        if (!voxelObject.TryGetChunk(voxelObject.ToChunkOffset(x, y, z), out _))
        {
            return;
        }

        ShapeLight shapeLight = voxel.GetShapeLight();
        voxel.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 15);
        lightingState.ToPropagate.Enqueue(new LightingState.VoxelLight(x, y, z, voxel));
    }
}
