using System.Collections.Generic;
using WaywardBeyond.Client.Numerics;
using WaywardBeyond.Voxels.Models;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Processing;

internal sealed class AmbientLightPass(LightingState lightingState, DepthState depthState) : VoxelObjectProcessor.IPass
{
    public void Process(VoxelObject voxelObject)
    {
        //  Seed ambient light at all min and max voxels at each depth plane
        
        foreach (KeyValuePair<Int2, Int2> pair in depthState.XY)
        {
            Int2 coords = pair.Key;
            Int2 depth = pair.Value;
            
            VoxelSample min = voxelObject.Sample(coords.X, coords.Y, depth.Min);
            ShapeLight shapeLight = min.Behind.ShapeLight;
            min.Behind.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 15);
            var light = new LightingState.VoxelLight(coords.X, coords.Y, depth.Min - 1, min.Behind);
            lightingState.ToPropagate.Enqueue(light);
            
            VoxelSample max = voxelObject.Sample(coords.X, coords.Y, depth.Max);
            shapeLight = max.Ahead.ShapeLight;
            max.Ahead.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 15);
            light = new LightingState.VoxelLight(coords.X, coords.Y, depth.Max + 1, max.Ahead);
            lightingState.ToPropagate.Enqueue(light);
        }
        
        foreach (KeyValuePair<Int2, Int2> pair in depthState.XZ)
        {
            Int2 coords = pair.Key;
            Int2 depth = pair.Value;
            
            VoxelSample min = voxelObject.Sample(coords.X, depth.Min, coords.Y);
            ShapeLight shapeLight = min.Below.ShapeLight;
            min.Below.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 15);
            var light = new LightingState.VoxelLight(coords.X, depth.Min - 1, coords.Y, min.Below);
            lightingState.ToPropagate.Enqueue(light);
            
            VoxelSample max = voxelObject.Sample(coords.X, depth.Max, coords.Y);
            shapeLight = max.Above.ShapeLight;
            max.Above.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 15);
            light = new LightingState.VoxelLight(coords.X, depth.Max + 1, coords.Y, max.Above);
            lightingState.ToPropagate.Enqueue(light);
        }
        
        foreach (KeyValuePair<Int2, Int2> pair in depthState.ZY)
        {
            Int2 coords = pair.Key;
            Int2 depth = pair.Value;
            
            VoxelSample min = voxelObject.Sample(depth.Min, coords.Y, coords.X);
            ShapeLight shapeLight = min.Left.ShapeLight;
            min.Left.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 15);
            var light = new LightingState.VoxelLight(depth.Min - 1, coords.Y, coords.X, min.Left);
            lightingState.ToPropagate.Enqueue(light);
            
            VoxelSample max = voxelObject.Sample(depth.Max, coords.Y, coords.X);
            shapeLight = max.Right.ShapeLight;
            max.Right.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 15);
            light = new LightingState.VoxelLight(depth.Max + 1, coords.Y, coords.X, max.Right);
            lightingState.ToPropagate.Enqueue(light);
        }
    }
}