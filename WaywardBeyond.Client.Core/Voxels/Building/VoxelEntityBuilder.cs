using System;
using System.Numerics;
using DryIoc;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Graphics;

namespace WaywardBeyond.Client.Core.Voxels.Building;

internal sealed class VoxelEntityBuilder(
    in ILogger logger,
    in Shader shader,
    in PBRTextureArrays textureArrays,
    in DataStore dataStore,
    in IContainer container,
    in IVoxelDecorator[] decorators
) {
    private readonly ILogger _logger = logger;
    private readonly DataStore _dataStore = dataStore;
    private readonly IVoxelDecorator[] _decorators = decorators;
    private readonly VoxelObjectBuilder _voxelObjectBuilder = new(container);
    
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
        
        _dataStore.AddOrUpdate(entity, transform);
        _dataStore.AddOrUpdate(entity, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous));
        
        _dataStore.AddOrUpdate(transparencyPtr, transform);
        _dataStore.AddOrUpdate(transparencyPtr, new ChildComponent(entity));
        
        VoxelObjectBuilder.Data data = _voxelObjectBuilder.Build(voxelObject);
        var voxelComponent = new VoxelComponent(voxelObject, transparencyPtr);
        UpdateEntity(entity, voxelComponent, data);
        
        return new Entity(entity, _dataStore);
    }
    
    public void Rebuild(int entity)
    {
        if (!_dataStore.TryGet(entity, out VoxelComponent voxelComponent))
        {
            _logger.LogWarning("Tried to rebuild entity {Entity} that doesn't have a VoxelComponent.", entity);
            return;
        }
        
        if (!_dataStore.TryGet(entity, out MeshRendererComponent opaqueRendererComponent) || !_dataStore.TryGet(voxelComponent.TransparencyPtr, out MeshRendererComponent transparentRendererComponent))
        {
            _logger.LogWarning("Tried to rebuild entity {Entity} but it is missing a MeshRendererComponent.", entity);
            return;
        }
        
        //  Cleanup existing renderers
        _dataStore.AddOrUpdate(entity, new MeshRendererCleanup(opaqueRendererComponent.MeshRenderer));
        _dataStore.AddOrUpdate(voxelComponent.TransparencyPtr, new MeshRendererCleanup(transparentRendererComponent.MeshRenderer));
        
        VoxelObjectBuilder.Data data = _voxelObjectBuilder.Build(voxelComponent.VoxelObject);
        UpdateEntity(entity, voxelComponent, data);
    }
    
    private void UpdateEntity(int entity, VoxelComponent voxelComponent, VoxelObjectBuilder.Data data)
    {
        var renderer = new MeshRenderer(data.OpaqueMesh, _opaqueMaterial, _renderOptions);
        _dataStore.AddOrUpdate(entity, new MeshRendererComponent(renderer));
        _dataStore.AddOrUpdate(entity, new ColliderComponent(data.CollisionShape));
        
        renderer = new MeshRenderer(data.TransparentMesh, _transparentMaterial, _transparentRenderOptions);
        _dataStore.AddOrUpdate(voxelComponent.TransparencyPtr, new MeshRendererComponent(renderer));
        
        _dataStore.Query<VoxelIdentifierComponent, ChildComponent>(0f, ForEachVoxelEntity);
        void ForEachVoxelEntity(float delta, DataStore store, int voxelEntity, ref VoxelIdentifierComponent voxelIdentifier, ref ChildComponent child)
        {
            if (child.Parent != entity)
            {
                return;
            }
            
            for (var i = 0; i < _decorators.Length; i++)
            {
                _decorators[i].Process(data, _dataStore, parent: entity, voxelEntity, voxelComponent, voxelIdentifier);
            }
            
            store.Free(voxelEntity);
        }
        
        for (var i = 0; i < _decorators.Length; i++)
        {
            _decorators[i].Process(data, _dataStore, entity, voxelComponent);
        }
    }
}