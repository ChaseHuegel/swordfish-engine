using System;
using System.Numerics;
using DryIoc;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Graphics;
using WaywardBeyond.Client.Core.Voxels.Processing;

namespace WaywardBeyond.Client.Core.Voxels.Building;

internal sealed class VoxelEntityBuilder(
    in ILogger logger,
    in Shader shader,
    in PBRTextureArrays textureArrays,
    in DataStore dataStore,
    in IContainer container
) {
    private readonly ILogger _logger = logger;
    private readonly DataStore _dataStore = dataStore;
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
        
        VoxelObjectBuilder.Data data = _voxelObjectBuilder.Build(_dataStore, entity, voxelObject);
        var voxelComponent = new VoxelComponent(voxelObject, transparencyPtr);
        UpdateEntity(entity, data, voxelComponent);
        
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
        
        VoxelObjectBuilder.Data data = _voxelObjectBuilder.Build(_dataStore, entity, voxelComponent.VoxelObject);
        UpdateEntity(entity, data, voxelComponent);
    }
    
    private void UpdateEntity(int entity, VoxelObjectBuilder.Data data, VoxelComponent voxelComponent)
    {
        var renderer = new MeshRenderer(data.OpaqueMesh, _opaqueMaterial, _renderOptions);
        _dataStore.AddOrUpdate(entity, new MeshRendererComponent(renderer));
        _dataStore.AddOrUpdate(entity, new ColliderComponent(data.CollisionShape));
        
        renderer = new MeshRenderer(data.TransparentMesh, _transparentMaterial, _transparentRenderOptions);
        _dataStore.AddOrUpdate(voxelComponent.TransparencyPtr, new MeshRendererComponent(renderer));
        
        //  TODO cleanup pre-existing lights and update existing ones
        for (var i = 0; i < data.LightSources.Count; i++)
        {
            LightingState.LightSource lightSource = data.LightSources[i];
            int lightEntity = _dataStore.Alloc();
            _dataStore.AddOrUpdate(lightEntity, new IdentifierComponent(name: null, tag: "game"));
            _dataStore.AddOrUpdate(lightEntity, new TransformComponent());
            _dataStore.AddOrUpdate(lightEntity, new VoxelIdentifierComponent(lightSource.X, lightSource.Y, lightSource.Z));
            _dataStore.AddOrUpdate(lightEntity, lightSource.Light);
            _dataStore.AddOrUpdate(lightEntity, new ChildComponent(lightEntity)
            {
                LocalPosition = new Vector3(lightSource.X, lightSource.Y, lightSource.Z),
            });
        }
    }
}