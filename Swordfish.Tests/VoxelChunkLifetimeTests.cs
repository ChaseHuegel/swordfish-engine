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
    [Fact]
    public void FreeChunksFreesEveryBucketEntityAndClearsTheRegistry()
    {
        var store = new DataStore();
        var voxelObject = new VoxelObject(16);
        var registry = new VoxelChunkRegistry();
        int parent = store.Alloc();
        store.AddOrUpdate(parent, new VoxelComponent(voxelObject, registry));
        store.AddOrUpdate(parent, new MeshRendererCleanup());

        var mesh = new Mesh([], [], [], [], []);
        var renderer = new MeshRenderer(mesh);
        int chunkEntity = store.Alloc();
        store.AddOrUpdate(chunkEntity, new MeshRendererComponent(renderer));
        var offset = new Short3(0, 0, 0);
        registry.OpaqueEntities[offset] = new ChunkEntity(chunkEntity, store.GetUuid(chunkEntity));

        VoxelEntityBuilder.FreeChunks(store, parent);

        Assert.Empty(registry.OpaqueEntities);
        Assert.Empty(registry.MaskedEntities);
        Assert.Empty(registry.TransparentEntities);
        Assert.False(store.Has(typeof(MeshRendererComponent), chunkEntity));
        Assert.Equal(Uuid.Null, store.GetUuid(chunkEntity));
        Assert.True(renderer.IsDisposed);
        Assert.True(mesh.IsDisposed);
    }

    [Fact]
    public void FreeChunksDoesNotTouchARecycledEntity()
    {
        var store = new DataStore();
        var voxelObject = new VoxelObject(16);
        var registry = new VoxelChunkRegistry();
        int parent = store.Alloc();
        store.AddOrUpdate(parent, new VoxelComponent(voxelObject, registry));

        int chunkEntity = store.Alloc();
        store.AddOrUpdate(chunkEntity, new MeshRendererComponent(new MeshRenderer(new Mesh([], [], [], [], []))));
        registry.OpaqueEntities[new Short3(0, 0, 0)] = new ChunkEntity(chunkEntity, store.GetUuid(chunkEntity));

        VoxelEntityBuilder.FreeChunks(store, parent);

        int recycled = store.Alloc();
        store.AddOrUpdate(recycled, new MeshRendererComponent(new MeshRenderer(new Mesh([], [], [], [], []))));

        VoxelEntityBuilder.FreeChunks(store, parent);

        Assert.True(store.Has(typeof(MeshRendererComponent), recycled));
    }

    [Fact]
    public void AStaleBucketEntryIsDroppedWithoutFreeingTheRecycledEntity()
    {
        var store = new DataStore();
        var voxelObject = new VoxelObject(16);
        var registry = new VoxelChunkRegistry();
        int parent = store.Alloc();
        store.AddOrUpdate(parent, new VoxelComponent(voxelObject, registry));

        int stale = store.Alloc();
        var staleUuid = store.GetUuid(stale);
        store.Free(stale);
        int recycled = store.Alloc();
        store.AddOrUpdate(recycled, new MeshRendererComponent(new MeshRenderer(new Mesh([], [], [], [], []))));

        var offset = new Short3(0, 0, 0);
        registry.OpaqueEntities[offset] = new ChunkEntity(recycled, staleUuid);

        VoxelEntityBuilder.FreeChunks(store, parent);

        Assert.Empty(registry.OpaqueEntities);
        Assert.True(store.Has(typeof(MeshRendererComponent), recycled));
    }
}
