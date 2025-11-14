using Swordfish.Library.Extensions;
using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class DepthPrePass(in DepthState depthState) 
    : VoxelObjectProcessor.ISamplePass
{
    private readonly DepthState _depthState = depthState;
    
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
        Int2 depth = _depthState.XY.GetOrAdd(key, DefaultDepthFactory);
        if (z < depth.Min)
        {
            depth.Min = z;
            _depthState.XY[key] = depth;
        }
        if (z > depth.Max)
        {
            depth.Max = z;
            _depthState.XY[key] = depth;
        }
        
        key = new Int2(x, z);
        depth = _depthState.XZ.GetOrAdd(key, DefaultDepthFactory);
        if (y < depth.Min)
        {
            depth.Min = y;
            _depthState.XZ[key] = depth;
        }
        if (y > depth.Max)
        {
            depth.Max = y;
            _depthState.XZ[key] = depth;
        }
        
        key = new Int2(z, y);
        depth = _depthState.ZY.GetOrAdd(key, DefaultDepthFactory);
        if (x < depth.Min)
        {
            depth.Min = x;
            _depthState.ZY[key] = depth;
        }
        if (x > depth.Max)
        {
            depth.Max = x;
            _depthState.ZY[key] = depth;
        }
    }
    
    private static Int2 DefaultDepthFactory()
    {
        return new Int2(int.MaxValue, int.MinValue);
    }
}