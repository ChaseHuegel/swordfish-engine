using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Graphics;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Voxels;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Voxels;
using Xunit;

namespace WaywardBeyond.Tests;

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

    [Fact]
    public void ChunkSortCenterSeparatesAdjacentChunksAlongExactBoundaryPlane()
    {
        Vector3 leftCenter = VoxelEntityBuilder.GetChunkSortCenter(new Short3(0, 0, 0), 16);
        Vector3 rightCenter = VoxelEntityBuilder.GetChunkSortCenter(new Short3(1, 0, 0), 16);

        Assert.Equal(new Vector3(7.5f, 7.5f, 7.5f), leftCenter);
        Assert.Equal(new Vector3(23.5f, 7.5f, 7.5f), rightCenter);

        //  Camera on the left side of the x = 15.5 separating plane sorts left chunk closer regardless of Z
        var cameraLeftOfBoundary = new Vector3(15.4f, 2f, 30f);
        Assert.True(Vector3.DistanceSquared(leftCenter, cameraLeftOfBoundary) < Vector3.DistanceSquared(rightCenter, cameraLeftOfBoundary));

        //  Camera on the right side of the x = 15.5 separating plane sorts right chunk closer regardless of Z
        var cameraRightOfBoundary = new Vector3(15.6f, 2f, -30f);
        Assert.True(Vector3.DistanceSquared(rightCenter, cameraRightOfBoundary) < Vector3.DistanceSquared(leftCenter, cameraRightOfBoundary));
    }
}
