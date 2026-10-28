using System.Numerics;
using Swordfish.ECS;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Voxels.Models;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Building;

internal class LightEntityDecorator : IVoxelEntityDecorator
{
    public void Process(in DataStore store, in int parent, in int entity, in VoxelComponent voxelComponent, in VoxelInfo voxelInfo)
    {
        ShapeLight shapeLight = voxelInfo.Voxel.ShapeLight;
        if (shapeLight.LightLevel <= 0)
        {
            return;
        }
        
        bool castShadows = shapeLight.LightLevel >= 14;
        var light = new LightComponent(radius: shapeLight.LightLevel, color: new Vector3(0.25f), size: 2.4f, castShadows: castShadows);
        store.AddOrUpdate(entity, light);
    }
}
