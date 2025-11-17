using System.Collections.Generic;
using System.Numerics;
using Swordfish.ECS;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Voxels.Processing;

namespace WaywardBeyond.Client.Core.Voxels.Building;

internal class LightDecorator : IVoxelDecorator
{
    private readonly HashSet<int> _updatedLightSources = [];
    
    public void Process(in VoxelObjectBuilder.Data data, in DataStore store, in int parent, in int entity, in VoxelComponent voxelComponent, in VoxelIdentifierComponent voxelIdentifier)
    {
        for (var i = 0; i < data.LightSources.Count; i++)
        {
            LightingState.LightSource lightSource = data.LightSources[i];
            if (lightSource.X != voxelIdentifier.X || lightSource.Y != voxelIdentifier.Y || lightSource.Z != voxelIdentifier.Z)
            {
                continue;
            }
            
            store.AddOrUpdate(entity, lightSource.Light);
            _updatedLightSources.Add(i);
            return;
        }
    }
    
    public void Process(in VoxelObjectBuilder.Data data, in DataStore store, in int entity, in VoxelComponent voxelComponent)
    {
        for (var i = 0; i < data.LightSources.Count; i++)
        {
            if (_updatedLightSources.Contains(i))
            {
                continue;
            }
            
            LightingState.LightSource lightSource = data.LightSources[i];
            
            //  TODO this step should be handled by the entity builder in a general way
            //  Init a voxel entity
            int voxelEntity = store.Alloc();
            store.AddOrUpdate(voxelEntity, new IdentifierComponent(name: null, tag: "game"));
            store.AddOrUpdate(voxelEntity, new VoxelIdentifierComponent(lightSource.X, lightSource.Y, lightSource.Z));
            store.AddOrUpdate(voxelEntity, new TransformComponent());
            store.AddOrUpdate(voxelEntity, new ChildComponent(entity)
            {
                LocalPosition = new Vector3(lightSource.X, lightSource.Y, lightSource.Z),
            });
            
            //  Init a light
            store.AddOrUpdate(voxelEntity, lightSource.Light);
        }
        
        _updatedLightSources.Clear();
    }
}