using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
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
    in DataStore dataStore,
    in IContainer container,
    in IVoxelEntityDecorator[] decorators
)
{
    private readonly ILogger _logger = logger;
    private readonly DataStore _dataStore = dataStore;
    private readonly IVoxelEntityDecorator[] _decorators = decorators;
    private readonly VoxelObjectBuilder _voxelObjectBuilder = new(container);
    
    private readonly Lock _rebuildLock = new();
    
    private readonly Lock _updatedEntitiesLock = new();
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

    public Entity Create(Guid guid, VoxelObject voxelObject, Vector3 position, Quaternion orientation, Vector3 scale)
    {
        int entity = _dataStore.Alloc(new IdentifierComponent(name: null, tag: "game"), new GuidComponent(guid));
        int transparencyPtr = _dataStore.Alloc(new IdentifierComponent(name: null, tag: "game"), new GuidComponent(guid));
        
        var transform = new TransformComponent(position, orientation, scale);
        var voxelComponent = new VoxelComponent(voxelObject, transparencyPtr);
        
        _dataStore.AddOrUpdate(entity, transform);
        _dataStore.AddOrUpdate(entity, voxelComponent);
        _dataStore.AddOrUpdate(entity, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous));
        _dataStore.AddOrUpdate(entity, new MeshRendererCleanup());
        
        _dataStore.AddOrUpdate(transparencyPtr, transform);
        _dataStore.AddOrUpdate(transparencyPtr, new ChildComponent(entity));
        
        VoxelObjectBuilder.Data data = _voxelObjectBuilder.Build(voxelObject);
        UpdateEntity(entity, voxelComponent, data);
        
        return new Entity(entity, _dataStore);
    }
    
    public void Rebuild(int entity)
    {
        using Lock.Scope _ = _rebuildLock.EnterScope();
        
        if (!_dataStore.TryGet(entity, out VoxelComponent voxelComponent))
        {
            _logger.LogWarning("Tried to rebuild entity {Entity} that doesn't have a VoxelComponent.", entity);
            return;
        }
        
        if (!_dataStore.TryGet(entity, out MeshRendererComponent opaqueRendererComponent) ||
            !_dataStore.TryGet(voxelComponent.TransparencyPtr, out MeshRendererComponent transparentRendererComponent))
        {
            _logger.LogWarning("Tried to rebuild entity {Entity} but it is missing a MeshRendererComponent.",
                entity);
            return;
        }
        
        if (!_dataStore.TryGet(entity, out MeshRendererCleanup meshRendererCleanup))
        {
            _logger.LogWarning("Tried to rebuild entity {Entity} but it is missing MeshRendererCleanup.", entity);
            return;
        }
        
        VoxelObjectBuilder.Data data = _voxelObjectBuilder.Build(voxelComponent.VoxelObject);
        UpdateEntity(entity, voxelComponent, data);
        
        //  Cleanup existing renderers
        meshRendererCleanup.MeshRenderers.Add(opaqueRendererComponent.MeshRenderer);
        meshRendererCleanup.MeshRenderers.Add(transparentRendererComponent.MeshRenderer);
    }
    
    private void UpdateEntity(int entity, VoxelComponent voxelComponent, VoxelObjectBuilder.Data data)
    {
        var renderer = new MeshRenderer(data.OpaqueMesh, _opaqueMaterial, _renderOptions);
        _dataStore.AddOrUpdate(entity, new MeshRendererComponent(renderer));
        _dataStore.AddOrUpdate(entity, new ColliderComponent(data.CollisionShape));
        
        renderer = new MeshRenderer(data.TransparentMesh, _transparentMaterial, _transparentRenderOptions);
        _dataStore.AddOrUpdate(voxelComponent.TransparencyPtr, new MeshRendererComponent(renderer));
     
        using Lock.Scope _ = _updatedEntitiesLock.EnterScope();
        
        //  Update any existing entities and cleanup old ones
        _dataStore.Query<VoxelIdentifierComponent, ChildComponent>(0f, ForEachVoxelEntity);
        void ForEachVoxelEntity(float delta, DataStore store, int voxelEntity, ref VoxelIdentifierComponent voxelIdentifier, ref ChildComponent child)
        {
            if (child.Parent != entity)
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
                    _decorators[n].Process(_dataStore, parent: entity, voxelEntity, voxelComponent, voxelInfo);
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
            
            int voxelEntity = _dataStore.Alloc();
            _dataStore.AddOrUpdate(voxelEntity, new IdentifierComponent(name: null, tag: "game"));
            _dataStore.AddOrUpdate(voxelEntity, new VoxelIdentifierComponent(voxelInfo.X, voxelInfo.Y, voxelInfo.Z));
            _dataStore.AddOrUpdate(voxelEntity, new TransformComponent());
            _dataStore.AddOrUpdate(voxelEntity, new ChildComponent(entity)
            {
                LocalPosition = new Vector3(voxelInfo.X, voxelInfo.Y, voxelInfo.Z),
            });
            
            for (var n = 0; n < _decorators.Length; n++)
            {
                _decorators[n].Process(_dataStore, parent: entity, voxelEntity, voxelComponent, voxelInfo);
            }
        }
        _updatedEntities.Clear();
    }
}