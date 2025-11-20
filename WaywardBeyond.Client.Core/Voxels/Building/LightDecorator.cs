using System.Numerics;
using Swordfish.ECS;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Voxels.Building;

internal class LightDecorator : IVoxelDecorator
{
    public void Process(in DataStore store, in int parent, in int entity, in VoxelComponent voxelComponent, in VoxelInfo voxelInfo)
    {
        ShapeLight shapeLight = voxelInfo.Voxel.ShapeLight;
        var light = new LightComponent(radius: shapeLight.LightLevel, color: new Vector3(0.25f), size: 2.5f);
        store.AddOrUpdate(entity, light);
    }
}