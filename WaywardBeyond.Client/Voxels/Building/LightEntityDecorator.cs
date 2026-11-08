using System.Numerics;
using Swordfish.ECS;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Voxels.Models;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Building;

internal sealed class LightEntityDecorator(IBrickDatabase brickDatabase) : IVoxelEntityDecorator
{
    public void Process(in DataStore store, in int parent, in int entity, in VoxelComponent voxelComponent, in VoxelInfo voxelInfo)
    {
        ShapeLight shapeLight = voxelInfo.Voxel.ShapeLight;
        if (shapeLight.LightLevel <= 0 || !IsExposed(voxelComponent.VoxelObject, in voxelInfo))
        {
            store.Remove<LightComponent>(entity);
            return;
        }

        bool castShadows = shapeLight.LightLevel >= 14;
        var light = new LightComponent(radius: shapeLight.LightLevel, color: new Vector3(0.25f), size: 2.4f, castShadows: castShadows);
        store.AddOrUpdate(entity, light);
    }

    private bool IsExposed(VoxelObject voxelObject, in VoxelInfo voxelInfo)
    {
        VoxelSample sample = voxelObject.Sample(voxelInfo.X, voxelInfo.Y, voxelInfo.Z);
        return !IsCuller(sample.Left)
            || !IsCuller(sample.Right)
            || !IsCuller(sample.Above)
            || !IsCuller(sample.Below)
            || !IsCuller(sample.Ahead)
            || !IsCuller(sample.Behind);
    }

    private bool IsCuller(in Voxel voxel)
    {
        return voxel.ID != 0 && brickDatabase.IsCuller(voxel, voxel.GetShapeLight().Shape);
    }
}
