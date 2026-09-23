using System;
using System.Collections.Generic;
using System.Numerics;
using DryIoc;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Graphics;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Voxels.Building;

internal sealed class VoxelEntityBuilder(
    in ILogger logger,
    in Shader shader,
    in PBRTextureArrays textureArrays,
    in IContainer container,
    in IVoxelEntityDecorator[] decorators
)
{
    private readonly ILogger _logger = logger;
    private readonly IVoxelEntityDecorator[] _decorators = decorators;
    private readonly VoxelObjectBuilder _voxelObjectBuilder = new(container);
    
    private readonly HashSet<int> _updatedEntities = [];

    private readonly Material _opaqueMaterial = new(shader, textureArrays.ToArray());
    
    private readonly Material _transparentMaterial = new(shader, textureArrays.ToArray())
    {
        Transparent = true,
    };

    private readonly RenderOptions _renderOptions = new()
    {
        DoubleFaced = false,
        Wireframe = false,
    };
    
    private readonly RenderOptions _transparentRenderOptions = new()
    {
        DoubleFaced = true,
        Wireframe = false,
    };

    public Entity Create(DataStore store, Uuid uuid, VoxelObject voxelObject, Vector3 position, Quaternion orientation, Vector3 scale)
    {
        int entity = store.Alloc(uuid);
        store.AddOrUpdate(entity, new IdentifierComponent(name: null, tag: "game"));
        int transparencyPtr = store.Alloc();
        store.AddOrUpdate(transparencyPtr, new IdentifierComponent(name: null, tag: "game"));
        
        var transform = new TransformComponent(position, orientation, scale);
        var voxelComponent = new VoxelComponent(voxelObject, store.GetUuid(transparencyPtr));
        
        store.AddOrUpdate(entity, transform);
        store.AddOrUpdate(entity, voxelComponent);
        store.AddOrUpdate(entity, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous));
        store.AddOrUpdate(entity, new MeshRendererCleanup());
        
        store.AddOrUpdate(transparencyPtr, transform);
        store.AddOrUpdate(transparencyPtr, new ChildComponent(store.GetUuid(entity)));
        
        VoxelObjectBuilder.Data data = _voxelObjectBuilder.Build(voxelObject);
        UpdateEntity(store, entity, voxelComponent, data);
        
        //  Clear the component dirty flag so the first-build isn't mistaken for a pending edit rebuild.
        store.ClearDirty<VoxelComponent>(entity);

        return new Entity(entity, store);
    }
    
    public void Rebuild(DataStore store, int entity)
    {
        if (!store.TryGet(entity, out VoxelComponent voxelComponent))
        {
            _logger.LogWarning("Tried to rebuild entity {Entity} that doesn't have a VoxelComponent.", entity);
            return;
        }
        
        if (!store.TryGet(entity, out MeshRendererComponent opaqueRendererComponent) ||
            !store.TryGet(voxelComponent.TransparencyPtr, out MeshRendererComponent transparentRendererComponent))
        {
            _logger.LogWarning("Tried to rebuild entity {Entity} but it is missing a MeshRendererComponent.",
                entity);
            return;
        }
        
        if (!store.TryGet(entity, out MeshRendererCleanup meshRendererCleanup))
        {
            _logger.LogWarning("Tried to rebuild entity {Entity} but it is missing MeshRendererCleanup.", entity);
            return;
        }
        
        VoxelObjectBuilder.Data data = _voxelObjectBuilder.Build(voxelComponent.VoxelObject);
        UpdateEntity(store, entity, voxelComponent, data);
        
        //  Cleanup existing renderers
        meshRendererCleanup.MeshRenderers.Add(opaqueRendererComponent.MeshRenderer);
        meshRendererCleanup.MeshRenderers.Add(transparentRendererComponent.MeshRenderer);
    }
    
    private void UpdateEntity(DataStore store, int entity, VoxelComponent voxelComponent, VoxelObjectBuilder.Data data)
    {
        var renderer = new MeshRenderer(data.OpaqueMesh, _opaqueMaterial, _renderOptions);
        store.AddOrUpdate(entity, new MeshRendererComponent(renderer));
        store.AddOrUpdate(entity, new ColliderComponent(data.CollisionShape));
        
        renderer = new MeshRenderer(data.TransparentMesh, _transparentMaterial, _transparentRenderOptions);
        if (store.TryGet(voxelComponent.TransparencyPtr, out int transparencyEntity))
        {
            store.AddOrUpdate(transparencyEntity, new MeshRendererComponent(renderer));
        }
        
        //  Update any existing entities and cleanup old ones
        store.Query<VoxelIdentifierComponent, ChildComponent>(0f, ForEachVoxelEntity);
        void ForEachVoxelEntity(float delta, DataStore store, int voxelEntity, in VoxelIdentifierComponent voxelIdentifier, in ChildComponent child)
        {
            if (child.Parent != store.GetUuid(entity))
            {
                return;
            }
            
            //  If this voxel entity still exists, update it.
            for (var i = 0; i < data.VoxelEntities.Count; i++)
            {
                VoxelInfo voxelInfo = data.VoxelEntities[i];
                if (voxelInfo.X != voxelIdentifier.X || voxelInfo.Y != voxelIdentifier.Y || voxelInfo.Z != voxelIdentifier.Z)
                {
                    continue;
                }
                
                for (var n = 0; n < _decorators.Length; n++)
                {
                    _decorators[n].Process(store, parent: entity, voxelEntity, voxelComponent, voxelInfo);
                }
                
                _updatedEntities.Add(i);
                return;
            }
            
            //  Else this entity isn't associated with a voxel anymore, free it.
            store.Free(voxelEntity);
        }
        
        //  Init any new entities
        for (var i = 0; i < data.VoxelEntities.Count; i++)
        {
            if (_updatedEntities.Contains(i))
            {
                continue;
            }
            
            VoxelInfo voxelInfo = data.VoxelEntities[i];
            
            int voxelEntity = store.Alloc();
            store.AddOrUpdate(voxelEntity, new IdentifierComponent(name: null, tag: "game"));
            store.AddOrUpdate(voxelEntity, new VoxelIdentifierComponent(voxelInfo.X, voxelInfo.Y, voxelInfo.Z));
            store.AddOrUpdate(voxelEntity, new TransformComponent());
            store.AddOrUpdate(voxelEntity, new ChildComponent(store.GetUuid(entity))
            {
                LocalPosition = new Vector3(voxelInfo.X, voxelInfo.Y, voxelInfo.Z),
            });
            
            for (var n = 0; n < _decorators.Length; n++)
            {
                _decorators[n].Process(store, parent: entity, voxelEntity, voxelComponent, voxelInfo);
            }
        }
        _updatedEntities.Clear();
    }
}