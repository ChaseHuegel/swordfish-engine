using Swordfish.ECS;
using Swordfish.Graphics;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Voxels;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Voxels;
using Xunit;

namespace Swordfish.Tests;

public class VoxelChunkLifetimeTests
{
    private static int AddBatchEntity(DataStore store, Uuid parentUuid, VoxelChunkRegistry registry, out MeshBatchRenderer renderer)
    {
        var batch = new MeshBatch();
        renderer = new MeshBatchRenderer(batch);
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new IdentifierComponent(name: null, tag: "game"));
        store.AddOrUpdate(entity, new MeshBatchRendererComponent(renderer));
        registry.BatchEntities.Add(new ChunkEntity(entity, store.GetUuid(entity)));
        return entity;
    }

    [Fact]
    public void FreeChunksFreesEveryBatchEntityAndClearsTheRegistry()
    {
        var store = new DataStore();
        var registry = new VoxelChunkRegistry();
        int parent = store.Alloc();
        Uuid parentUuid = store.GetUuid(parent);
        store.AddOrUpdate(parent, new VoxelComponent(new VoxelObject(16), registry));

        int batchEntity = AddBatchEntity(store, parentUuid, registry, out MeshBatchRenderer renderer);
        registry.OpaqueRanges[new Short3(0, 0, 0)] = new MeshRangeHandle(0, 1);

        VoxelEntityBuilder.FreeChunks(store, parent);

        Assert.Empty(registry.BatchEntities);
        Assert.Empty(registry.OpaqueRanges);
        Assert.Null(registry.OpaqueBatch);
        Assert.False(store.Has(typeof(MeshBatchRendererComponent), batchEntity));
        Assert.Equal(Uuid.Null, store.GetUuid(batchEntity));
        Assert.True(renderer.IsDisposed);
        Assert.True(renderer.MeshBatch.IsDisposed);
    }

    [Fact]
    public void FreeChunksDoesNotTouchARecycledEntity()
    {
        var store = new DataStore();
        var registry = new VoxelChunkRegistry();
        int parent = store.Alloc();
        Uuid parentUuid = store.GetUuid(parent);
        store.AddOrUpdate(parent, new VoxelComponent(new VoxelObject(16), registry));

        int batchEntity = AddBatchEntity(store, parentUuid, registry, out _);
        store.Free(batchEntity);

        int recycled = store.Alloc();
        store.AddOrUpdate(recycled, new MeshBatchRendererComponent(new MeshBatchRenderer(new MeshBatch())));

        VoxelEntityBuilder.FreeChunks(store, parent);

        Assert.True(store.Has(typeof(MeshBatchRendererComponent), recycled));
    }

    [Fact]
    public void AStaleBatchEntryIsDroppedWithoutFreeingTheRecycledEntity()
    {
        var store = new DataStore();
        var registry = new VoxelChunkRegistry();
        int parent = store.Alloc();
        Uuid parentUuid = store.GetUuid(parent);
        store.AddOrUpdate(parent, new VoxelComponent(new VoxelObject(16), registry));

        int stale = store.Alloc();
        Uuid staleUuid = store.GetUuid(stale);
        store.Free(stale);
        int recycled = store.Alloc();
        store.AddOrUpdate(recycled, new MeshBatchRendererComponent(new MeshBatchRenderer(new MeshBatch())));
        registry.BatchEntities.Add(new ChunkEntity(recycled, staleUuid));

        VoxelEntityBuilder.FreeChunks(store, parent);

        Assert.Empty(registry.BatchEntities);
        Assert.True(store.Has(typeof(MeshBatchRendererComponent), recycled));
    }
}
