using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Library.Extensions;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class ThrusterPostPass(in IBrickDatabase brickDatabase, in LightingState lightingState) : VoxelObjectProcessor.ISamplePass
{
    private static readonly Vector3 _lightColor = Color.FromArgb(244, 126, 27).ToVector3() * 20;
    
    private readonly IBrickDatabase _brickDatabase = brickDatabase;
    private readonly LightingState _lightingState = lightingState;
    private readonly HashSet<ushort> _thrusterBrickIDs = [..brickDatabase.Get(info => info.Tags.Contains("thruster")).Select(info => info.DataID)];
    
    public VoxelObjectProcessor.Stage Stage => VoxelObjectProcessor.Stage.PostPass;
    
    public bool ShouldProcessChunk(ChunkData chunkData)
    {
        foreach (ushort id in _thrusterBrickIDs)
        {
            if (chunkData.Palette.Any(id))
            {
                return true;
            }
        }

        return false;
    }
    
    public void Process(VoxelSample sample)
    {
        if (!_thrusterBrickIDs.Contains(sample.Center.ID))
        {
            return;
        }
        
        int x = sample.Coords.X + sample.ChunkOffset.X;
        int y = sample.Coords.Y + sample.ChunkOffset.Y;
        int z = sample.Coords.Z + sample.ChunkOffset.Z;
        var light = new LightComponent(radius: 0.75f, color: _lightColor, size: 0.25f);
        
        var lightSource = new LightingState.LightSource(x, y, z, light);
        _lightingState.Sources.Add(lightSource);
    }
}