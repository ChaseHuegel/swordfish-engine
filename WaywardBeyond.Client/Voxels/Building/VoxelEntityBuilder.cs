using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Types.Shapes;
using Swordfish.Physics;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Graphics;
using WaywardBeyond.Client.Voxels.Models;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Building;

internal sealed class VoxelEntityBuilder(
    in ILogger logger,
    in Shader shader,
    in PBRTextureArrays textureArrays,
    in VoxelObjectBuilder voxelObjectBuilder,
    in IVoxelEntityDecorator[] decorators,
    in VoxelSettings voxelSettings
) {
    private readonly ILogger _logger = logger;
    private readonly IVoxelEntityDecorator[] _decorators = decorators;
    private readonly VoxelObjectBuilder _voxelObjectBuilder = voxelObjectBuilder;
    private readonly VoxelSettings _voxelSettings = voxelSettings;
    
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
        store.AddOrUpdate(entity, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous));

        _voxelObjectBuilder.InitializeLighting(voxelObject, registry.DepthState);
        registry.DepthStateInitialized = true;

        int vertexHint = (int)Math.Min(1_000_000L, Math.Max(4096L, (long)voxelObject.ChunkCount * 2048L));
        int indexHint = (int)Math.Min(2_000_000L, Math.Max(8192L, (long)vertexHint * 2L));
        registry.OpaqueBatch = new MeshBatch(vertexHint, indexHint);
        registry.MaskedBatch = new MeshBatch(vertexHint, indexHint);
        registry.TransparentBatch = new MeshBatch(vertexHint, indexHint);
        Uuid parentUuid = store.GetUuid(entity);

        using VoxelObjectBuilder.ChunkBuildContext buildContext = _voxelObjectBuilder.OpenChunkBuildContext();
        foreach (ChunkData chunk in voxelObject)
        {
            registry.KnownChunks.Add(chunk.Coords);

            if (chunk.Palette.Only(0))
            {
                continue;
            }

            UpdateChunkRanges(store, entity, parentUuid, voxelComponent, chunk, buildContext, reconcileEntities: true);

            ChunkCollisionData chunkCollision = _voxelObjectBuilder.BuildChunkCollision(buildContext, chunk);
            if (!chunkCollision.IsEmpty)
            {
                registry.ChunkColliders[chunk.Coords] = chunkCollision;
            }
        }

        //  Attach the renderers only after the ranges are filled. The scene does not see a
        //  partial batch during the load build.
        AttachBatchRenderer(store, parentUuid, in transform, registry.OpaqueBatch, _opaqueMaterial, _opaqueRenderOptions, registry);
        AttachBatchRenderer(store, parentUuid, in transform, registry.MaskedBatch, _maskedMaterial, _maskedRenderOptions, registry);
        AttachBatchRenderer(store, parentUuid, in transform, registry.TransparentBatch, _transparentMaterial, _transparentRenderOptions, registry);

        CompoundShape collisionShape = VoxelObjectBuilder.CombineCollisions(registry.ChunkColliders.Values);
        store.AddOrUpdate(entity, voxelComponent);
        store.AddOrUpdate(entity, new ColliderComponent(collisionShape));

        // Clear the component dirty flag so the first-build isn't mistaken for a pending edit rebuild.
        store.ClearDirty<VoxelComponent>(entity);

        return new Entity(entity, store);
    }

    private static void AttachBatchRenderer(
        DataStore store,
        Uuid parentUuid,
        in TransformComponent transform,
        MeshBatch? batch,
        Material material,
        RenderOptions renderOptions,
        VoxelChunkRegistry registry
    )
    {
        if (batch == null)
        {
            return;
        }

        var renderer = new MeshBatchRenderer(batch, material, renderOptions);
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new IdentifierComponent(name: null, tag: "game"));
        store.AddOrUpdate(entity, transform);
        store.AddOrUpdate(entity, new ChildComponent(parentUuid));
        store.AddOrUpdate(entity, new MeshBatchRendererComponent(renderer));
        registry.BatchEntities.Add(new ChunkEntity(entity, store.GetUuid(entity)));
    }

    /// <summary>Rebuilds dirty chunks within the configured time budget. Returns true when no dirty chunks remain.</summary>
    public bool Rebuild(DataStore store, int entity)
    {
        if (!store.TryGet(entity, out VoxelComponent voxelComponent))
        {
            _logger.LogWarning("Tried to rebuild entity {Entity} that doesn't have a VoxelComponent.", entity);
            return true;
        }

        VoxelChunkRegistry registry = voxelComponent.Registry;
        Uuid parentUuid = store.GetUuid(entity);

        if (registry.DirtyChunks.Count == 0)
        {
            //  A first-build dirty flag can arrive before the build finishes. There is nothing to do.
            _logger.LogDebug("Tried to rebuild entity {Entity} without any dirty chunks.", entity);
            return true;
        }

        using VoxelObjectBuilder.ChunkBuildContext buildContext = _voxelObjectBuilder.OpenChunkBuildContext();
        return RebuildDirtyChunks(store, entity, parentUuid, voxelComponent, buildContext);
    }

    private bool RebuildDirtyChunks(
        DataStore store,
        int parentEntity,
        Uuid parentUuid,
        VoxelComponent voxelComponent,
        VoxelObjectBuilder.ChunkBuildContext buildContext
    )
    {
        VoxelChunkRegistry registry = voxelComponent.Registry;
        VoxelObject voxelObject = voxelComponent.VoxelObject;
        long startedAt = Stopwatch.GetTimestamp();
        double budgetMs = _voxelSettings.RebuildBudgetMs.Get();

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
        var collisionChanged = false;
        for (var i = 0; i < dirtyList.Count; i++)
        {
            Short3 chunkOffset = dirtyList[i];
            bool geometryDirty = registry.GeometryDirtyChunks.Contains(chunkOffset);

            if (!voxelObject.TryGetChunk(chunkOffset, out ChunkData chunk) || chunk.Palette.Only(0))
            {
                RemoveChunkRanges(registry, chunkOffset);
                if (geometryDirty)
                {
                    ReconcileVoxelEntities(store, parentEntity, parentUuid, voxelComponent, chunkOffset, []);
                }

                if (registry.ChunkColliders.Remove(chunkOffset))
                {
                    collisionChanged = true;
                }
            }
            else
            {
                UpdateChunkRanges(store, parentEntity, parentUuid, voxelComponent, chunk, buildContext, geometryDirty);

                if (geometryDirty)
                {
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
            }

            MarkProcessed(registry, chunkOffset);
            if (Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds >= budgetMs)
            {
                break;
            }
        }

        if (collisionChanged)
        {
            CompoundShape collisionShape = VoxelObjectBuilder.CombineCollisions(registry.ChunkColliders.Values);
            store.AddOrUpdate(parentEntity, new ColliderComponent(collisionShape));
        }

        return registry.DirtyChunks.Count == 0;
    }

    private void UpdateChunkRanges(
        DataStore store,
        int parentEntity,
        Uuid parentUuid,
        VoxelComponent voxelComponent,
        ChunkData chunk,
        VoxelObjectBuilder.ChunkBuildContext buildContext,
        bool reconcileEntities
    )
    {
        VoxelObjectBuilder.ChunkMeshData data = _voxelObjectBuilder.BuildChunk(buildContext, chunk);
        VoxelChunkRegistry registry = voxelComponent.Registry;
        Short3 chunkOffset = chunk.Coords;

        UpdateBatch(registry.OpaqueBatch, registry.OpaqueRanges, chunkOffset, data.OpaqueMesh);
        UpdateBatch(registry.MaskedBatch, registry.MaskedRanges, chunkOffset, data.MaskedMesh);
        UpdateBatch(registry.TransparentBatch, registry.TransparentRanges, chunkOffset, data.TransparentMesh);

        if (reconcileEntities)
        {
            ReconcileVoxelEntities(store, parentEntity, parentUuid, voxelComponent, chunkOffset, data.VoxelEntities);
        }
    }

    private static void UpdateBatch(
        MeshBatch? batch,
        Dictionary<Short3, MeshRangeHandle> ranges,
        Short3 chunkOffset,
        in VoxelObjectBuilder.ChunkMesh mesh
    )
    {
        if (batch == null)
        {
            return;
        }

        if (!mesh.IsEmpty)
        {
            if (ranges.TryGetValue(chunkOffset, out MeshRangeHandle handle))
            {
                batch.UpdateRange(handle, mesh.VertexData, mesh.VertexCount, mesh.Indices, mesh.Bounds);
            }
            else
            {
                ranges[chunkOffset] = batch.AddRange(mesh.VertexData, mesh.VertexCount, mesh.Indices, mesh.Bounds);
            }
        }
        else if (ranges.Remove(chunkOffset, out MeshRangeHandle handle))
        {
            batch.RemoveRange(handle);
        }
    }

    private static void RemoveChunkRanges(VoxelChunkRegistry registry, Short3 chunkOffset)
    {
        RemoveBatchRange(registry.OpaqueBatch, registry.OpaqueRanges, chunkOffset);
        RemoveBatchRange(registry.MaskedBatch, registry.MaskedRanges, chunkOffset);
        RemoveBatchRange(registry.TransparentBatch, registry.TransparentRanges, chunkOffset);
    }

    private static void RemoveBatchRange(MeshBatch? batch, Dictionary<Short3, MeshRangeHandle> ranges, Short3 chunkOffset)
    {
        if (batch != null && ranges.Remove(chunkOffset, out MeshRangeHandle handle))
        {
            batch.RemoveRange(handle);
        }
    }

    private static void MarkProcessed(VoxelChunkRegistry registry, Short3 chunkOffset)
    {
        registry.DirtyChunks.Remove(chunkOffset);
        registry.GeometryDirtyChunks.Remove(chunkOffset);
        registry.LightDirtyChunks.Remove(chunkOffset);
    }

    /// <summary>Frees every batch renderer and range that belongs to the voxel entity.</summary>
    public static void FreeChunks(DataStore store, int entity)
    {
        if (!store.TryGet(entity, out VoxelComponent voxelComponent))
        {
            return;
        }

        VoxelChunkRegistry registry = voxelComponent.Registry;

        if (store.TryGet(entity, out MeshRendererCleanup cleanup))
        {
            cleanup.DisposeAll();
        }

        for (var i = 0; i < registry.BatchEntities.Count; i++)
        {
            ChunkEntity entry = registry.BatchEntities[i];
            if (entry.Uuid == Uuid.Null || store.GetUuid(entry.Entity) != entry.Uuid)
            {
                continue;
            }

            if (store.TryGet(entry.Entity, out MeshBatchRendererComponent component) && component.Renderer != null)
            {
                component.Renderer.Dispose();
                component.Renderer.MeshBatch.Dispose();
            }

            store.Free(entry.Entity);
        }

        registry.BatchEntities.Clear();
        registry.OpaqueRanges.Clear();
        registry.MaskedRanges.Clear();
        registry.TransparentRanges.Clear();
        registry.OpaqueBatch = null;
        registry.MaskedBatch = null;
        registry.TransparentBatch = null;
        registry.DirtyChunks.Clear();
        registry.GeometryDirtyChunks.Clear();
        registry.LightDirtyChunks.Clear();
        registry.KnownChunks.Clear();
        registry.ChunkColliders.Clear();
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
