using System.Collections.Generic;
using Swordfish.Library.Extensions;
using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class AmbientLightPass(in LightingState lightingState) : VoxelObjectProcessor.IPass
{
    private readonly LightingState _lightingState = lightingState;
    
    public void Process(VoxelObject voxelObject)
    {
        var xy = new Dictionary<Int2, Int2>();
        var xz = new Dictionary<Int2, Int2>();
        var zy = new Dictionary<Int2, Int2>();
        
        foreach (ChunkData chunk in voxelObject)
        {
            foreach (VoxelSample sample in chunk.GetSampler())
            {
                if (sample.Center.ID == 0)
                {
                    continue;
                }
                
                int x = sample.Coords.X + sample.ChunkOffset.X;
                int y = sample.Coords.Y + sample.ChunkOffset.Y;
                int z = sample.Coords.Z + sample.ChunkOffset.Z;
                
                var key = new Int2(x, y);
                Int2 depth = xy.GetOrAdd(key, DefaultDepthFactory);
                if (z < depth.Min)
                {
                    depth.Min = z;
                    xy[key] = depth;
                }
                if (z > depth.Max)
                {
                    depth.Max = z;
                    xy[key] = depth;
                }
                
                key = new Int2(x, z);
                depth = xz.GetOrAdd(key, DefaultDepthFactory);
                if (y < depth.Min)
                {
                    depth.Min = y;
                    xz[key] = depth;
                }
                if (y > depth.Max)
                {
                    depth.Max = y;
                    xz[key] = depth;
                }
                
                key = new Int2(z, y);
                depth = zy.GetOrAdd(key, DefaultDepthFactory);
                if (x < depth.Min)
                {
                    depth.Min = x;
                    zy[key] = depth;
                }
                if (x > depth.Max)
                {
                    depth.Max = x;
                    zy[key] = depth;
                }
            }
        }

        foreach (KeyValuePair<Int2, Int2> pair in xy)
        {
            Int2 coords = pair.Key;
            Int2 depth = pair.Value;
            
            VoxelSample min = voxelObject.Sample(coords.X, coords.Y, depth.Min);
            ShapeLight shapeLight = min.Ahead.ShapeLight;
            min.Ahead.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 15);
            var light = new LightingState.VoxelLight(coords.X, coords.Y, depth.Min - 1, min.Ahead);
            _lightingState.Lights.Enqueue(light);
            
            VoxelSample max = voxelObject.Sample(coords.X, coords.Y, depth.Max);
            shapeLight = max.Behind.ShapeLight;
            max.Behind.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 15);
            light = new LightingState.VoxelLight(coords.X, coords.Y, depth.Max + 1, max.Behind);
            _lightingState.Lights.Enqueue(light);
        }
        
        foreach (KeyValuePair<Int2, Int2> pair in xz)
        {
            Int2 coords = pair.Key;
            Int2 depth = pair.Value;
            
            VoxelSample min = voxelObject.Sample(coords.X, depth.Min, coords.Y);
            ShapeLight shapeLight = min.Below.ShapeLight;
            min.Below.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 15);
            var light = new LightingState.VoxelLight(coords.X, depth.Min - 1, coords.Y, min.Below);
            _lightingState.Lights.Enqueue(light);
            
            VoxelSample max = voxelObject.Sample(coords.X, depth.Max, coords.Y);
            shapeLight = max.Above.ShapeLight;
            max.Above.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 15);
            light = new LightingState.VoxelLight(coords.X, depth.Max + 1, coords.Y, max.Above);
            _lightingState.Lights.Enqueue(light);
        }
        
        foreach (KeyValuePair<Int2, Int2> pair in zy)
        {
            Int2 coords = pair.Key;
            Int2 depth = pair.Value;
            
            VoxelSample min = voxelObject.Sample(depth.Min, coords.Y, coords.X);
            ShapeLight shapeLight = min.Left.ShapeLight;
            min.Left.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 15);
            var light = new LightingState.VoxelLight(depth.Min - 1, coords.Y, coords.X, min.Left);
            _lightingState.Lights.Enqueue(light);
            
            VoxelSample max = voxelObject.Sample(depth.Max, coords.Y, coords.X);
            shapeLight = max.Right.ShapeLight;
            max.Right.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 15);
            light = new LightingState.VoxelLight(depth.Max + 1, coords.Y, coords.X, max.Right);
            _lightingState.Lights.Enqueue(light);
        }
    }

    private static Int2 DefaultDepthFactory()
    {
        return new Int2(int.MaxValue, int.MinValue);
    }
}