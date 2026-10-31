using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Types.Shapes;
using Swordfish.Physics;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Graphics;
using WaywardBeyond.Client.Voxels;
using WaywardBeyond.Client.Voxels.Models;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Building;

internal sealed class VoxelEntityBuilder(
    in ILogger logger,
    in Shader shader,
    in PBRTextureArrays textureArrays,
    in VoxelObjectBuilder voxelObjectBuilder,
    in IVoxelEntityDecorator[] decorators
)
{
    private readonly ILogger _logger = logger;
    private readonly IVoxelEntityDecorator[] _decorators = decorators;
    private readonly VoxelObjectBuilder _voxelObjectBuilder = voxelObjectBuilder;
    
    [ThreadStatic]
    private static HashSet<int>? _updatedEntities;
    private static HashSet<int> UpdatedEntities => _updatedEntities ??= [];

    private readonly Material _opaqueMaterial = new(shader, textureArrays.ToArray());
    private readonly Material _maskedMaterial = new(shader, textureArrays.ToArray());
    private readonly Material _transparentMaterial = new(shader, textureArrays.ToArray())
    {
        Transparent = true,
    };

    private readonly RenderOptions _opaqueRenderOptions = new()
    {
        AlphaMode = AlphaMode.Opaque,
        DoubleFaced = false,
        Wireframe = false,
        CastShadows = true,
        ReceiveShadows = true,
    };

    private readonly RenderOptions _maskedRenderOptions = new()
    {
        AlphaMode = AlphaMode.Masked,
        DoubleFaced = true,
        Wireframe = false,
        CastShadows = true,
        ReceiveShadows = true,
    };
    
    private readonly RenderOptions _transparentRenderOptions = new()
    {
        AlphaMode = AlphaMode.Blend,
        DoubleFaced = true,
        Wireframe = false,
        CastShadows = false,
        ReceiveShadows = true,
    };

    public Entity Create(DataStore store, Uuid uuid, VoxelObject voxelObject, Vector3 position, Quaternion orientation, Vector3 scale)
    {
        int entity = store.Alloc(uuid);
        store.AddOrUpdate(entity, new IdentifierComponent(name: null, tag: "game"));
        
        var transform = new TransformComponent(position, orientation, scale);
        var registry = new VoxelChunkRegistry();
        var voxelComponent = new VoxelComponent(voxelObject, registry);
        
        store.AddOrUpdate(entity, transform);
        store.AddOrUpdate(entity, voxelComponent);
        store.AddOrUpdate(entity, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous));
        store.AddOrUpdate(entity, new MeshRendererCleanup());

        _voxelObjectBuilder.InitializeLighting(voxelObject, registry.DepthState);
        registry.DepthStateInitialized = true;

        Uuid parentUuid = store.GetUuid(entity);
        using VoxelObjectBuilder.ChunkBuildContext buildContext = _voxelObjectBuilder.OpenChunkBuildContext();
        foreach (ChunkData chunk in voxelObject)
        {
            registry.KnownChunks.Add(chunk.Coords);

            if (chunk.Palette.Only(0))
            {
                continue;
            }

            BuildAndAttachChunk(store, entity, parentUuid, voxelComponent, chunk, buildContext);

            ChunkCollisionData chunkCollision = _voxelObjectBuilder.BuildChunkCollision(buildContext, chunk);
            if (!chunkCollision.IsEmpty)
            {
                voxelComponent.Registry.ChunkColliders[chunk.Coords] = chunkCollision;
            }
        }

        CompoundShape collisionShape = VoxelObjectBuilder.CombineCollisions(voxelComponent.Registry.ChunkColliders.Values);
        store.AddOrUpdate(entity, new ColliderComponent(collisionShape));
        
        // Clear the component dirty flag so the first-build isn't mistaken for a pending edit rebuild.
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
        
        if (!store.TryGet(entity, out MeshRendererCleanup meshRendererCleanup))
        {
            _logger.LogWarning("Tried to rebuild entity {Entity} but it is missing MeshRendererCleanup.", entity);
            return;
        }

        VoxelChunkRegistry registry = voxelComponent.Registry;
        Uuid parentUuid = store.GetUuid(entity);

        if (registry.DirtyChunks.Count == 0)
        {
            _logger.LogWarning("Tried to rebuild entity {Entity} without any dirty chunks.", entity);
            return;
        }

        using VoxelObjectBuilder.ChunkBuildContext buildContext = _voxelObjectBuilder.OpenChunkBuildContext();
        RebuildDirtyChunks(store, entity, parentUuid, voxelComponent, meshRendererCleanup, buildContext);
    }

    private void RebuildDirtyChunks(
        DataStore store,
        int parentEntity,
        Uuid parentUuid,
        VoxelComponent voxelComponent,
        MeshRendererCleanup meshRendererCleanup,
        VoxelObjectBuilder.ChunkBuildContext buildContext
    )
    {
        VoxelChunkRegistry registry = voxelComponent.Registry;
        VoxelObject voxelObject = voxelComponent.VoxelObject;

        if (registry.ChunkColliders.Count == 0 && voxelObject.ChunkCount > 0 && registry.GeometryDirtyChunks.Count > 0)
        {
            foreach (ChunkData residentChunk in voxelObject)
            {
                if (!residentChunk.Palette.Only(0))
                {
                    ChunkCollisionData c = _voxelObjectBuilder.BuildChunkCollision(buildContext, residentChunk);
                    if (!c.IsEmpty)
                    {
                        registry.ChunkColliders[residentChunk.Coords] = c;
                    }
                }
            }
        }

        var dirtyList = new List<Short3>(registry.DirtyChunks);
        registry.DirtyChunks.Clear();

        var collisionChanged = false;
        for (var i = 0; i < dirtyList.Count; i++)
        {
            Short3 chunkOffset = dirtyList[i];
            bool geometryDirty = registry.GeometryDirtyChunks.Contains(chunkOffset);

            if (!voxelObject.TryGetChunk(chunkOffset, out ChunkData chunk) || chunk.Palette.Only(0))
            {
                RemoveChunkRenderers(store, chunkOffset, registry, meshRendererCleanup);
                if (geometryDirty)
                {
                    ReconcileVoxelEntities(store, parentEntity, parentUuid, voxelComponent, chunkOffset, []);
                }
                if (registry.ChunkColliders.Remove(chunkOffset))
                {
                    collisionChanged = true;
                }
                continue;
            }

            UpdateChunkRenderers(store, parentEntity, parentUuid, voxelComponent, chunk, meshRendererCleanup, buildContext, geometryDirty);

            if (!geometryDirty)
            {
                continue;
            }

            ChunkCollisionData newCollision = _voxelObjectBuilder.BuildChunkCollision(buildContext, chunk);
            if (newCollision.IsEmpty)
            {
                if (registry.ChunkColliders.Remove(chunkOffset))
                {
                    collisionChanged = true;
                }
            }
            else
            {
                registry.ChunkColliders[chunkOffset] = newCollision;
                collisionChanged = true;
            }
        }

        registry.GeometryDirtyChunks.Clear();
        registry.LightDirtyChunks.Clear();

        if (collisionChanged)
        {
            CompoundShape collisionShape = VoxelObjectBuilder.CombineCollisions(registry.ChunkColliders.Values);
            store.AddOrUpdate(parentEntity, new ColliderComponent(collisionShape));
        }
    }

    private void BuildAndAttachChunk(
        DataStore store,
        int parentEntity,
        Uuid parentUuid,
        VoxelComponent voxelComponent,
        ChunkData chunk,
        VoxelObjectBuilder.ChunkBuildContext buildContext
    )
    {
        VoxelObjectBuilder.ChunkMeshData data = _voxelObjectBuilder.BuildChunk(buildContext, chunk);
        VoxelChunkRegistry registry = voxelComponent.Registry;
        Short3 chunkOffset = chunk.Coords;

        if (data.OpaqueMesh != null)
        {
            int opaqueEntity = store.Alloc();
            store.AddOrUpdate(opaqueEntity, new IdentifierComponent(name: null, tag: "game"));
            store.AddOrUpdate(opaqueEntity, new TransformComponent());
            store.AddOrUpdate(opaqueEntity, new ChildComponent(parentUuid));
            store.AddOrUpdate(opaqueEntity, new VoxelChunkComponent(chunkOffset, AlphaMode.Opaque));
            store.AddOrUpdate(opaqueEntity, new MeshRendererComponent(new MeshRenderer(data.OpaqueMesh, _opaqueMaterial, _opaqueRenderOptions)));
            registry.OpaqueEntities[chunkOffset] = opaqueEntity;
        }

        if (data.MaskedMesh != null)
        {
            int maskedEntity = store.Alloc();
            store.AddOrUpdate(maskedEntity, new IdentifierComponent(name: null, tag: "game"));
            store.AddOrUpdate(maskedEntity, new TransformComponent());
            store.AddOrUpdate(maskedEntity, new ChildComponent(parentUuid));
            store.AddOrUpdate(maskedEntity, new VoxelChunkComponent(chunkOffset, AlphaMode.Masked));
            store.AddOrUpdate(maskedEntity, new MeshRendererComponent(new MeshRenderer(data.MaskedMesh, _maskedMaterial, _maskedRenderOptions)));
            registry.MaskedEntities[chunkOffset] = maskedEntity;
        }

        if (data.TransparentMesh != null)
        {
            int transparentEntity = store.Alloc();
            store.AddOrUpdate(transparentEntity, new IdentifierComponent(name: null, tag: "game"));
            store.AddOrUpdate(transparentEntity, new TransformComponent());
            store.AddOrUpdate(transparentEntity, new ChildComponent(parentUuid));
            store.AddOrUpdate(transparentEntity, new VoxelChunkComponent(chunkOffset, AlphaMode.Blend));
            store.AddOrUpdate(transparentEntity, new MeshRendererComponent(new MeshRenderer(data.TransparentMesh, _transparentMaterial, _transparentRenderOptions)));
            registry.TransparentEntities[chunkOffset] = transparentEntity;
        }

        ReconcileVoxelEntities(store, parentEntity, parentUuid, voxelComponent, chunkOffset, data.VoxelEntities);
    }

    private void UpdateChunkRenderers(
        DataStore store,
        int parentEntity,
        Uuid parentUuid,
        VoxelComponent voxelComponent,
        ChunkData chunk,
        MeshRendererCleanup meshRendererCleanup,
        VoxelObjectBuilder.ChunkBuildContext buildContext,
        bool reconcileEntities = true
    )
    {
        VoxelObjectBuilder.ChunkMeshData data = _voxelObjectBuilder.BuildChunk(buildContext, chunk);
        VoxelChunkRegistry registry = voxelComponent.Registry;
        Short3 chunkOffset = chunk.Coords;

        UpdateRendererBucket(store, parentUuid, chunkOffset, data.OpaqueMesh, _opaqueMaterial, _opaqueRenderOptions, AlphaMode.Opaque, registry.OpaqueEntities, meshRendererCleanup);
        UpdateRendererBucket(store, parentUuid, chunkOffset, data.MaskedMesh, _maskedMaterial, _maskedRenderOptions, AlphaMode.Masked, registry.MaskedEntities, meshRendererCleanup);
        UpdateRendererBucket(store, parentUuid, chunkOffset, data.TransparentMesh, _transparentMaterial, _transparentRenderOptions, AlphaMode.Blend, registry.TransparentEntities, meshRendererCleanup);

        if (reconcileEntities)
        {
            ReconcileVoxelEntities(store, parentEntity, parentUuid, voxelComponent, chunkOffset, data.VoxelEntities);
        }
    }

    private void UpdateRendererBucket(
        DataStore store,
        Uuid parentUuid,
        Short3 chunkOffset,
        Mesh? mesh,
        Material material,
        RenderOptions renderOptions,
        AlphaMode alphaMode,
        Dictionary<Short3, int> bucket,
        MeshRendererCleanup meshRendererCleanup
    )
    {
        if (mesh != null)
        {
            var newRenderer = new MeshRenderer(mesh, material, renderOptions);
            if (bucket.TryGetValue(chunkOffset, out int entity))
            {
                if (store.TryGet(entity, out MeshRendererComponent oldComp) && oldComp.MeshRenderer != null)
                {
                    meshRendererCleanup.Pending.Enqueue(new MeshRendererCleanup.PendingEntry(oldComp.MeshRenderer, newRenderer));
                }
                store.AddOrUpdate(entity, new MeshRendererComponent(newRenderer));
            }
            else
            {
                entity = store.Alloc();
                store.AddOrUpdate(entity, new IdentifierComponent(name: null, tag: "game"));
                store.AddOrUpdate(entity, new TransformComponent());
                store.AddOrUpdate(entity, new ChildComponent(parentUuid));
                store.AddOrUpdate(entity, new VoxelChunkComponent(chunkOffset, alphaMode));
                store.AddOrUpdate(entity, new MeshRendererComponent(newRenderer));
                bucket[chunkOffset] = entity;
            }
        }
        else if (bucket.Remove(chunkOffset, out int entity))
        {
            if (store.TryGet(entity, out MeshRendererComponent oldComp) && oldComp.MeshRenderer != null)
            {
                meshRendererCleanup.Pending.Enqueue(new MeshRendererCleanup.PendingEntry(oldComp.MeshRenderer, null));
            }
            store.Free(entity);
        }
    }

    private static void RemoveChunkRenderers(
        DataStore store,
        Short3 chunkOffset,
        VoxelChunkRegistry registry,
        MeshRendererCleanup meshRendererCleanup
    )
    {
        RemoveBucket(store, chunkOffset, registry.OpaqueEntities, meshRendererCleanup);
        RemoveBucket(store, chunkOffset, registry.MaskedEntities, meshRendererCleanup);
        RemoveBucket(store, chunkOffset, registry.TransparentEntities, meshRendererCleanup);
    }

    private static void RemoveBucket(
        DataStore store,
        Short3 chunkOffset,
        Dictionary<Short3, int> bucket,
        MeshRendererCleanup meshRendererCleanup
    )
    {
        if (bucket.Remove(chunkOffset, out int entity))
        {
            if (store.TryGet(entity, out MeshRendererComponent comp) && comp.MeshRenderer != null)
            {
                meshRendererCleanup.Pending.Enqueue(new MeshRendererCleanup.PendingEntry(comp.MeshRenderer, null));
            }
            store.Free(entity);
        }
    }

    private void ReconcileVoxelEntities(
        DataStore store,
        int parentEntity,
        Uuid parentUuid,
        VoxelComponent voxelComponent,
        Short3 chunkOffset,
        List<VoxelInfo> chunkVoxelEntities
    )
    {
        HashSet<int> updatedEntities = UpdatedEntities;
        updatedEntities.Clear();

        byte chunkSize = voxelComponent.VoxelObject.ChunkSize;
        int chunkShift = int.TrailingZeroCount(chunkSize);

        store.Query<VoxelIdentifierComponent, ChildComponent>(0f, ForEachVoxelEntity);
        void ForEachVoxelEntity(float delta, DataStore s, int voxelEntity, in VoxelIdentifierComponent voxelIdentifier, in ChildComponent child)
        {
            if (child.Parent != parentUuid)
            {
                return;
            }

            var entityChunk = new Short3(
                (short)(voxelIdentifier.X >> chunkShift),
                (short)(voxelIdentifier.Y >> chunkShift),
                (short)(voxelIdentifier.Z >> chunkShift)
            );

            if (entityChunk != chunkOffset)
            {
                return;
            }

            for (var i = 0; i < chunkVoxelEntities.Count; i++)
            {
                VoxelInfo voxelInfo = chunkVoxelEntities[i];
                if (voxelInfo.X != voxelIdentifier.X || voxelInfo.Y != voxelIdentifier.Y || voxelInfo.Z != voxelIdentifier.Z)
                {
                    continue;
                }

                for (var n = 0; n < _decorators.Length; n++)
                {
                    _decorators[n].Process(store, parent: parentEntity, voxelEntity, voxelComponent, voxelInfo);
                }

                updatedEntities.Add(i);
                return;
            }

            store.Free(voxelEntity);
        }

        for (var i = 0; i < chunkVoxelEntities.Count; i++)
        {
            if (updatedEntities.Contains(i))
            {
                continue;
            }

            VoxelInfo voxelInfo = chunkVoxelEntities[i];

            int voxelEntity = store.Alloc();
            store.AddOrUpdate(voxelEntity, new IdentifierComponent(name: null, tag: "game"));
            store.AddOrUpdate(voxelEntity, new VoxelIdentifierComponent(voxelInfo.X, voxelInfo.Y, voxelInfo.Z));
            store.AddOrUpdate(voxelEntity, new TransformComponent());
            store.AddOrUpdate(voxelEntity, new ChildComponent(parentUuid)
            {
                LocalPosition = new Vector3(voxelInfo.X, voxelInfo.Y, voxelInfo.Z),
            });

            for (var n = 0; n < _decorators.Length; n++)
            {
                _decorators[n].Process(store, parent: parentEntity, voxelEntity, voxelComponent, voxelInfo);
            }
        }

        updatedEntities.Clear();
    }
}
