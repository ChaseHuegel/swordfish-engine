using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Types.Shapes;
using Swordfish.Physics;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Voxels.Models;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Building;

internal sealed class VoxelEntityBuilder(
    in ILogger logger,
    in VoxelMaterials materials,
    in VoxelObjectBuilder voxelObjectBuilder,
    in IVoxelEntityDecorator[] decorators,
    in VoxelSettings voxelSettings
) {
    private readonly ILogger _logger = logger;
    private readonly VoxelMaterials _materials = materials;
    private readonly IVoxelEntityDecorator[] _decorators = decorators;
    private readonly VoxelObjectBuilder _voxelObjectBuilder = voxelObjectBuilder;
    private readonly VoxelSettings _voxelSettings = voxelSettings;
    
    [ThreadStatic]
    private static HashSet<int>? _updatedEntities;
    private static HashSet<int> UpdatedEntities => _updatedEntities ??= [];

    [ThreadStatic]
    private static List<Short3>? _dirtyList;
    private static List<Short3> DirtyList => _dirtyList ??= [];

    /// <summary>Creates a voxel entity with batch renderers, decorators, and optional physics collision.</summary>
    public Entity Create(
        DataStore store,
        Uuid uuid,
        VoxelObject voxelObject,
        Vector3 position,
        Quaternion orientation,
        Vector3 scale,
        string tag = "game",
        bool withPhysics = true
    )
    {
        int entity = store.Alloc(uuid);
        store.AddOrUpdate(entity, new IdentifierComponent(name: null, tag: tag));

        var transform = new TransformComponent(position, orientation, scale);
        var registry = new VoxelChunkRegistry();
        var voxelComponent = new VoxelComponent(voxelObject, registry);

        store.AddOrUpdate(entity, transform);
        if (withPhysics)
        {
            store.AddOrUpdate(entity, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous));
        }

        _voxelObjectBuilder.InitializeLighting(voxelObject, registry.DepthState);
        registry.DepthStateInitialized = true;

        int vertexHint = (int)Math.Min(1_000_000L, Math.Max(4096L, (long)voxelObject.ChunkCount * 2048L));
        int indexHint = (int)Math.Min(2_000_000L, Math.Max(8192L, (long)vertexHint * 2L));
        registry.OpaqueBatch = new MeshBatch(vertexHint, indexHint);
        registry.OpaqueNoShadowBatch = new MeshBatch(vertexHint, indexHint);
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

            UpdateChunkRanges(store, entity, parentUuid, voxelComponent, chunk, buildContext, reconcileEntities: true, tag: tag);

            if (withPhysics)
            {
                ChunkCollisionData chunkCollision = _voxelObjectBuilder.BuildChunkCollision(buildContext, chunk);
                if (!chunkCollision.IsEmpty)
                {
                    registry.ChunkColliders[chunk.Coords] = chunkCollision;
                }
            }
        }

        //  Attach the renderers only after the ranges are filled. The scene does not see a
        //  partial batch during the load build.
        AttachBatchRenderer(store, parentUuid, in transform, registry.OpaqueBatch, _materials.Opaque, _materials.OpaqueOptions, registry, tag);
        AttachBatchRenderer(store, parentUuid, in transform, registry.OpaqueNoShadowBatch, _materials.Opaque, _materials.OpaqueNoShadowOptions, registry, tag);
        AttachBatchRenderer(store, parentUuid, in transform, registry.MaskedBatch, _materials.Masked, _materials.MaskedOptions, registry, tag);
        AttachBatchRenderer(store, parentUuid, in transform, registry.TransparentBatch, _materials.Transparent, _materials.TransparentOptions, registry, tag);

        store.AddOrUpdate(entity, voxelComponent);
        if (withPhysics)
        {
            CompoundShape collisionShape = VoxelObjectBuilder.CombineCollisions(registry.ChunkColliders.Values);
            store.AddOrUpdate(entity, new ColliderComponent(collisionShape));
        }

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
        VoxelChunkRegistry registry,
        string tag
    )
    {
        if (batch == null)
        {
            return;
        }

        var renderer = new MeshBatchRenderer(batch, material, renderOptions);
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new IdentifierComponent(name: null, tag: tag));
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

        List<Short3> dirtyList = DirtyList;
        dirtyList.Clear();
        dirtyList.AddRange(registry.DirtyChunks);
        var collisionChanged = false;
        for (var i = 0; i < dirtyList.Count; i++)
        {
            Short3 chunkOffset = dirtyList[i];
            bool geometryDirty = registry.GeometryDirtyChunks.Contains(chunkOffset);
            bool entityDirty = geometryDirty || registry.EntityDirtyChunks.Contains(chunkOffset);

            if (!voxelObject.TryGetChunk(chunkOffset, out ChunkData chunk) || chunk.Palette.Only(0))
            {
                RemoveChunkRanges(registry, chunkOffset);
                if (entityDirty)
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
                UpdateChunkRanges(store, parentEntity, parentUuid, voxelComponent, chunk, buildContext, entityDirty);

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
        bool reconcileEntities,
        string tag = "game"
    )
    {
        VoxelObjectBuilder.ChunkMeshData data = _voxelObjectBuilder.BuildChunk(buildContext, chunk);
        VoxelChunkRegistry registry = voxelComponent.Registry;
        Short3 chunkOffset = chunk.Coords;
        Vector3 chunkSortCenter = GetChunkSortCenter(chunkOffset, voxelComponent.VoxelObject.ChunkSize);

        UpdateBatch(registry.OpaqueBatch, registry.OpaqueRanges, chunkOffset, data.OpaqueMesh, chunkSortCenter);
        UpdateBatch(registry.OpaqueNoShadowBatch, registry.OpaqueNoShadowRanges, chunkOffset, data.OpaqueNoShadowMesh, chunkSortCenter);
        UpdateBatch(registry.MaskedBatch, registry.MaskedRanges, chunkOffset, data.MaskedMesh, chunkSortCenter);
        UpdateBatch(registry.TransparentBatch, registry.TransparentRanges, chunkOffset, data.TransparentMesh, chunkSortCenter);

        if (reconcileEntities)
        {
            ReconcileVoxelEntities(store, parentEntity, parentUuid, voxelComponent, chunkOffset, data.VoxelEntities, tag);
        }
    }

    /// <summary>Returns the uniform grid-cell center of a chunk for back-to-front sorting across chunk boundaries.</summary>
    internal static Vector3 GetChunkSortCenter(Short3 chunkOffset, byte chunkSize)
    {
        float halfSpan = (chunkSize - 1) * 0.5f;
        return new Vector3(
            chunkOffset.X * chunkSize + halfSpan,
            chunkOffset.Y * chunkSize + halfSpan,
            chunkOffset.Z * chunkSize + halfSpan
        );
    }

    private static void UpdateBatch(
        MeshBatch? batch,
        Dictionary<Short3, MeshRangeHandle> ranges,
        Short3 chunkOffset,
        in VoxelObjectBuilder.ChunkMesh mesh,
        in Vector3 sortCenter
    )
    {
        if (batch == null)
        {
            return;
        }

        if (!mesh.IsEmpty)
        {
            ReadOnlySpan<float> vertices = mesh.VertexData.AsSpan(0, mesh.VertexCount * MeshBatch.VERTEX_STRIDE);
            ReadOnlySpan<uint> indices = mesh.Indices.AsSpan(0, mesh.IndexCount);
            if (ranges.TryGetValue(chunkOffset, out MeshRangeHandle handle))
            {
                batch.UpdateRange(handle, vertices, mesh.VertexCount, indices, mesh.Bounds, sortCenter);
            }
            else
            {
                ranges[chunkOffset] = batch.AddRange(vertices, mesh.VertexCount, indices, mesh.Bounds, sortCenter);
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
        RemoveBatchRange(registry.OpaqueNoShadowBatch, registry.OpaqueNoShadowRanges, chunkOffset);
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
        registry.EntityDirtyChunks.Remove(chunkOffset);
    }

    /// <summary>Frees every batch child entity and range that belongs to the voxel entity.</summary>
    public static void FreeChunks(DataStore store, int entity)
    {
        if (!store.TryGet(entity, out VoxelComponent voxelComponent))
        {
            return;
        }

        VoxelChunkRegistry registry = voxelComponent.Registry;

        for (var i = 0; i < registry.BatchEntities.Count; i++)
        {
            ChunkEntity entry = registry.BatchEntities[i];
            if (entry.Uuid == Uuid.Null || store.GetUuid(entry.Entity) != entry.Uuid)
            {
                continue;
            }

            //  Free captures the batch renderer removal; the engine releases the renderer and batch.
            store.Free(entry.Entity);
        }

        registry.BatchEntities.Clear();
        registry.OpaqueRanges.Clear();
        registry.OpaqueNoShadowRanges.Clear();
        registry.MaskedRanges.Clear();
        registry.TransparentRanges.Clear();
        registry.OpaqueBatch = null;
        registry.OpaqueNoShadowBatch = null;
        registry.MaskedBatch = null;
        registry.TransparentBatch = null;
        registry.DirtyChunks.Clear();
        registry.GeometryDirtyChunks.Clear();
        registry.LightDirtyChunks.Clear();
        registry.EntityDirtyChunks.Clear();
        registry.ChunksWithEntities.Clear();
        registry.KnownChunks.Clear();
        registry.ChunkColliders.Clear();
    }

    /// <summary>Frees a voxel entity together with its batch renderers and decorator children.</summary>
    public static void Destroy(DataStore store, int entity)
    {
        Uuid parentUuid = store.GetUuid(entity);
        FreeChunks(store, entity);

        if (parentUuid != Uuid.Null)
        {
            var freeChildren = new FreeChildrenAction { ParentUuid = parentUuid };
            store.Query<ChildComponent, FreeChildrenAction>(0f, ref freeChildren);
        }

        store.Free(entity);
    }

    private struct FreeChildrenAction : IForEach<ChildComponent>
    {
        public Uuid ParentUuid;

        public void Execute(float delta, DataStore store, int entity, in ChildComponent child)
        {
            if (child.Parent != ParentUuid)
            {
                return;
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
        List<VoxelInfo> chunkVoxelEntities,
        string tag = "game"
    )
    {
        if (chunkVoxelEntities.Count == 0 && !voxelComponent.Registry.ChunksWithEntities.Contains(chunkOffset))
        {
            return;
        }

        HashSet<int> updatedEntities = UpdatedEntities;
        updatedEntities.Clear();

        byte chunkSize = voxelComponent.VoxelObject.ChunkSize;
        int chunkShift = int.TrailingZeroCount(chunkSize);

        var action = new ReconcileEntityAction
        {
            Owner = this,
            ParentEntity = parentEntity,
            ParentUuid = parentUuid,
            VoxelComponent = voxelComponent,
            ChunkOffset = chunkOffset,
            ChunkShift = chunkShift,
            ChunkVoxelEntities = chunkVoxelEntities,
            UpdatedEntities = updatedEntities,
        };
        store.Query<VoxelIdentifierComponent, ChildComponent, ReconcileEntityAction>(0f, ref action);

        for (var i = 0; i < chunkVoxelEntities.Count; i++)
        {
            if (updatedEntities.Contains(i))
            {
                continue;
            }

            VoxelInfo voxelInfo = chunkVoxelEntities[i];

            int voxelEntity = store.Alloc();
            store.AddOrUpdate(voxelEntity, new IdentifierComponent(name: null, tag: tag));
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

        if (chunkVoxelEntities.Count > 0)
        {
            voxelComponent.Registry.ChunksWithEntities.Add(chunkOffset);
        }
        else
        {
            voxelComponent.Registry.ChunksWithEntities.Remove(chunkOffset);
        }

        updatedEntities.Clear();
    }

    private struct ReconcileEntityAction : IForEach<VoxelIdentifierComponent, ChildComponent>
    {
        public VoxelEntityBuilder Owner;
        public int ParentEntity;
        public Uuid ParentUuid;
        public VoxelComponent VoxelComponent;
        public Short3 ChunkOffset;
        public int ChunkShift;
        public List<VoxelInfo> ChunkVoxelEntities;
        public HashSet<int> UpdatedEntities;

        public void Execute(float delta, DataStore store, int voxelEntity, in VoxelIdentifierComponent voxelIdentifier, in ChildComponent child)
        {
            if (child.Parent != ParentUuid)
            {
                return;
            }

            var entityChunk = new Short3(
                (short)(voxelIdentifier.X >> ChunkShift),
                (short)(voxelIdentifier.Y >> ChunkShift),
                (short)(voxelIdentifier.Z >> ChunkShift)
            );

            if (entityChunk != ChunkOffset)
            {
                return;
            }

            for (var i = 0; i < ChunkVoxelEntities.Count; i++)
            {
                VoxelInfo voxelInfo = ChunkVoxelEntities[i];
                if (voxelInfo.X != voxelIdentifier.X || voxelInfo.Y != voxelIdentifier.Y || voxelInfo.Z != voxelIdentifier.Z)
                {
                    continue;
                }

                for (var n = 0; n < Owner._decorators.Length; n++)
                {
                    Owner._decorators[n].Process(store, parent: ParentEntity, voxelEntity, VoxelComponent, voxelInfo);
                }

                UpdatedEntities.Add(i);
                return;
            }

            store.Free(voxelEntity);
        }
    }
}
