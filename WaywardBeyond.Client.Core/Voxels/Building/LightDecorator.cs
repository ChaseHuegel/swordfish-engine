using System.Collections.Generic;
using System.Numerics;
using Swordfish.ECS;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Voxels.Models;
using WaywardBeyond.Client.Core.Voxels.Processing;

namespace WaywardBeyond.Client.Core.Voxels.Building;

internal class LightDecorator : IVoxelDecorator
{
    private readonly HashSet<int> _updatedLightSources = [];
    
    public void Process(in VoxelObjectBuilder.Data data, in DataStore store, in int parent, in int entity, in VoxelComponent voxelComponent, in VoxelIdentifierComponent voxelIdentifier)
    {
        for (var i = 0; i < data.VoxelEntities.Count; i++)
        {
            VoxelInfo voxelInfo = data.VoxelEntities[i];
            if (voxelInfo.X != voxelIdentifier.X || voxelInfo.Y != voxelIdentifier.Y || voxelInfo.Z != voxelIdentifier.Z)
            {
                continue;
            }
            
            
            ShapeLight shapeLight = voxelInfo.Voxel.ShapeLight;
            var light = new LightComponent(radius: shapeLight.LightLevel, color: new Vector3(0.25f), size: 2.5f);
            store.AddOrUpdate(entity, light);
            _updatedLightSources.Add(i);
            return;
        }
    }
    
    public void Process(in VoxelObjectBuilder.Data data, in DataStore store, in int entity, in VoxelComponent voxelComponent)
    {
        for (var i = 0; i < data.VoxelEntities.Count; i++)
        {
            if (_updatedLightSources.Contains(i))
            {
                continue;
            }
            
            VoxelInfo voxelInfo = data.VoxelEntities[i];
            
            //  TODO this step should be handled by the entity builder in a general way
            //  Init a voxel entity
            int ptr = store.Alloc();
            store.AddOrUpdate(ptr, new IdentifierComponent(name: null, tag: "game"));
            store.AddOrUpdate(ptr, new VoxelIdentifierComponent(voxelInfo.X, voxelInfo.Y, voxelInfo.Z));
            store.AddOrUpdate(ptr, new TransformComponent());
            store.AddOrUpdate(ptr, new ChildComponent(entity)
            {
                LocalPosition = new Vector3(voxelInfo.X, voxelInfo.Y, voxelInfo.Z),
            });
            
            //  Init a light
            ShapeLight shapeLight = voxelInfo.Voxel.ShapeLight;
            var light = new LightComponent(radius: shapeLight.LightLevel, color: new Vector3(0.25f), size: 2.5f);
            store.AddOrUpdate(ptr, light);
        }
        
        _updatedLightSources.Clear();
    }
}