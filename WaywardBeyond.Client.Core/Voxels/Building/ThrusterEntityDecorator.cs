using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Library.Extensions;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Voxels.Building;

internal class ThrusterEntityDecorator(in IBrickDatabase brickDatabase) : IVoxelEntityDecorator
{
    private static readonly Vector3 _lightColor = Color.FromArgb(244, 126, 27).ToVector3() * 20;
    
    private readonly HashSet<ushort> _thrusterBrickIDs = [..brickDatabase.Get(info => info.Tags.Contains("thruster")).Select(info => info.DataID)];
    
    public void Process(in DataStore store, in int parent, in int entity, in VoxelComponent voxelComponent, in VoxelInfo voxelInfo)
    {
        if (!_thrusterBrickIDs.Contains(voxelInfo.Voxel.ID))
        {
            return;
        }
        
        var light = new LightComponent(radius: 0.75f, color: _lightColor, size: 0.25f);
        store.AddOrUpdate(entity, light);
    }
}