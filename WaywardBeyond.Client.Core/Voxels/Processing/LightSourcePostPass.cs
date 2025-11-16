using System.Numerics;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Bricks;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class LightSourcePostPass(in IBrickDatabase brickDatabase, in LightingState lightingState) 
    : LightPrePass(brickDatabase), VoxelObjectProcessor.ISamplePass
{
    private readonly LightingState _lightingState = lightingState;
    
    public VoxelObjectProcessor.Stage Stage => VoxelObjectProcessor.Stage.PostPass;
    
    public void Process(VoxelSample sample)
    {
        Result<BrickInfo> brickInfoResult = BrickDatabase.Get(sample.Center.ID);
        BrickInfo brickInfo = brickInfoResult.Value;
        if (!brickInfoResult.Success || !brickInfo.LightSource)
        {
            return;
        }
        
        int x = sample.Coords.X + sample.ChunkOffset.X;
        int y = sample.Coords.Y + sample.ChunkOffset.Y;
        int z = sample.Coords.Z + sample.ChunkOffset.Z;
        var light = new LightComponent(radius: brickInfo.Brightness, color: new Vector3(0.25f), size: 2.5f);
        
        var lightSource = new LightingState.LightSource(x, y, z, light);
        _lightingState.Sources.Add(lightSource);
    }
}