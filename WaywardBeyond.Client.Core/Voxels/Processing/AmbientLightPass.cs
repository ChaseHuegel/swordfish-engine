using System.Collections.Generic;
using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class AmbientLightPass(LightingState lightingState, DepthState depthState) : VoxelObjectProcessor.IPass
{
    private readonly LightingState _lightingState = lightingState;
    private readonly DepthState _depthState = depthState;
    
    public void Process(VoxelObject voxelObject)
    {
        //  Seed ambient light at all min and max voxels at each depth plane
        
        foreach (KeyValuePair<Int2, Int2> pair in _depthState.XY)
        {
            Int2 coords = pair.Key;
            Int2 depth = pair.Value;
            
            VoxelSample min = voxelObject.Sample(coords.X, coords.Y, depth.Min);
            ShapeLight shapeLight = min.Ahead.ShapeLight;
            min.Ahead.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 15);
            var light = new LightingState.VoxelLight(coords.X, coords.Y, depth.Min - 1, min.Ahead);
            _lightingState.ToPropagate.Enqueue(light);
            
            VoxelSample max = voxelObject.Sample(coords.X, coords.Y, depth.Max);
            shapeLight = max.Behind.ShapeLight;
            max.Behind.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 15);
            light = new LightingState.VoxelLight(coords.X, coords.Y, depth.Max + 1, max.Behind);
            _lightingState.ToPropagate.Enqueue(light);
        }
        
        foreach (KeyValuePair<Int2, Int2> pair in _depthState.XZ)
        {
            Int2 coords = pair.Key;
            Int2 depth = pair.Value;
            
            VoxelSample min = voxelObject.Sample(coords.X, depth.Min, coords.Y);
            ShapeLight shapeLight = min.Below.ShapeLight;
            min.Below.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 15);
            var light = new LightingState.VoxelLight(coords.X, depth.Min - 1, coords.Y, min.Below);
            _lightingState.ToPropagate.Enqueue(light);
            
            VoxelSample max = voxelObject.Sample(coords.X, depth.Max, coords.Y);
            shapeLight = max.Above.ShapeLight;
            max.Above.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 15);
            light = new LightingState.VoxelLight(coords.X, depth.Max + 1, coords.Y, max.Above);
            _lightingState.ToPropagate.Enqueue(light);
        }
        
        foreach (KeyValuePair<Int2, Int2> pair in _depthState.ZY)
        {
            Int2 coords = pair.Key;
            Int2 depth = pair.Value;
            
            VoxelSample min = voxelObject.Sample(depth.Min, coords.Y, coords.X);
            ShapeLight shapeLight = min.Left.ShapeLight;
            min.Left.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 15);
            var light = new LightingState.VoxelLight(depth.Min - 1, coords.Y, coords.X, min.Left);
            _lightingState.ToPropagate.Enqueue(light);
            
            VoxelSample max = voxelObject.Sample(depth.Max, coords.Y, coords.X);
            shapeLight = max.Right.ShapeLight;
            max.Right.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 15);
            light = new LightingState.VoxelLight(depth.Max + 1, coords.Y, coords.X, max.Right);
            _lightingState.ToPropagate.Enqueue(light);
        }
    }
}