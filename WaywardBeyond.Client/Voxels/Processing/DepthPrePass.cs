using Swordfish.Library.Extensions;
using WaywardBeyond.Client.Numerics;
using WaywardBeyond.Voxels.Models;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Processing;

internal sealed class DepthPrePass(DepthState depthState) : VoxelObjectProcessor.ISamplePass
{
    public VoxelObjectProcessor.Stage Stage => VoxelObjectProcessor.Stage.PrePass;

    public bool ShouldProcessChunk(ChunkData chunkData)
    {
        return !chunkData.Palette.Only(id: 0);
    }

    public void Process(VoxelSample sample)
    {
        if (sample.Center.ID == 0)
        {
            return;
        }
        
        int x = sample.Coords.X + sample.ChunkOffset.X;
        int y = sample.Coords.Y + sample.ChunkOffset.Y;
        int z = sample.Coords.Z + sample.ChunkOffset.Z;
        
        var key = new Int2(x, y);
        Int2 depth = depthState.XY.GetOrAdd(key, DefaultDepthFactory);
        if (z < depth.Min)
        {
            depth.Min = z;
            depthState.XY[key] = depth;
        }
        if (z > depth.Max)
        {
            depth.Max = z;
            depthState.XY[key] = depth;
        }
        
        key = new Int2(x, z);
        depth = depthState.XZ.GetOrAdd(key, DefaultDepthFactory);
        if (y < depth.Min)
        {
            depth.Min = y;
            depthState.XZ[key] = depth;
        }
        if (y > depth.Max)
        {
            depth.Max = y;
            depthState.XZ[key] = depth;
        }
        
        key = new Int2(z, y);
        depth = depthState.ZY.GetOrAdd(key, DefaultDepthFactory);
        if (x < depth.Min)
        {
            depth.Min = x;
            depthState.ZY[key] = depth;
        }
        if (x > depth.Max)
        {
            depth.Max = x;
            depthState.ZY[key] = depth;
        }
    }
    
    private static Int2 DefaultDepthFactory()
    {
        return new Int2(int.MaxValue, int.MinValue);
    }
}