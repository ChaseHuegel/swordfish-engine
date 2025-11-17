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
        int ptr = _dataStore.Alloc(new IdentifierComponent(name: null, tag: "game"), new GuidComponent(guid));
        int transparencyPtr = _dataStore.Alloc(new IdentifierComponent(name: null, tag: "game"), new GuidComponent(guid));
        
        VoxelObjectBuilder.Data data = _voxelObjectBuilder.Build(_dataStore, ptr, voxelObject);
        
        var transform = new TransformComponent(position, orientation, scale);
        var renderer = new MeshRenderer(data.OpaqueMesh, _opaqueMaterial, _renderOptions);
        _dataStore.AddOrUpdate(ptr, transform);
        _dataStore.AddOrUpdate(ptr, new MeshRendererComponent(renderer));
        _dataStore.AddOrUpdate(ptr, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous));
        _dataStore.AddOrUpdate(ptr, new ColliderComponent(data.CollisionShape));
        _dataStore.AddOrUpdate(ptr, new VoxelComponent(voxelObject, transparencyPtr));
        
        renderer = new MeshRenderer(data.TransparentMesh, _transparentMaterial, _transparentRenderOptions);
        _dataStore.AddOrUpdate(transparencyPtr, transform);
        _dataStore.AddOrUpdate(transparencyPtr, new MeshRendererComponent(renderer));
        _dataStore.AddOrUpdate(transparencyPtr, new ChildComponent(ptr));
        
        //  TODO need to handle light entities
        
        return new Entity(ptr, _dataStore);
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

        VoxelObjectBuilder.Data data = _voxelObjectBuilder.Build(_dataStore, entity, voxelComponent.VoxelObject);

        var renderer = new MeshRenderer(data.OpaqueMesh, _opaqueMaterial, _renderOptions);
        _dataStore.AddOrUpdate(entity, new MeshRendererComponent(renderer));
        _dataStore.AddOrUpdate(entity, new ColliderComponent(data.CollisionShape));
        _dataStore.AddOrUpdate(entity, new MeshRendererCleanup(opaqueRendererComponent.MeshRenderer));
        
        renderer = new MeshRenderer(data.TransparentMesh, _transparentMaterial, _transparentRenderOptions);
        _dataStore.AddOrUpdate(voxelComponent.TransparencyPtr, new MeshRendererComponent(renderer));
        _dataStore.AddOrUpdate(voxelComponent.TransparencyPtr, new MeshRendererCleanup(transparentRendererComponent.MeshRenderer));
        
        //  TODO need to handle light entities
    }
}