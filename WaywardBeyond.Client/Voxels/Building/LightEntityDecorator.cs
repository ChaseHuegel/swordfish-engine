using System.Numerics;
using Swordfish.ECS;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Voxels.Models;

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
        
        var light = new LightComponent(radius: shapeLight.LightLevel, color: new Vector3(0.25f), size: 2.5f);
        store.AddOrUpdate(entity, light);
    }
}