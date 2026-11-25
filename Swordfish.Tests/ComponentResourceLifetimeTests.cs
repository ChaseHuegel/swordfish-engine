using System.Numerics;
using DryIoc;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Types.Shapes;
using Swordfish.Physics;
using Swordfish.Physics.Jolt;
using Swordfish.Settings;
using Xunit;

namespace Swordfish.Tests;

/// <summary>Pins engine-owned release of native resources held by components.</summary>
public class ComponentResourceLifetimeTests
{
    [Fact]
    public void RemovedRendererRetiresAfterRemoval()
    {
        var processor = new MeshRendererComponentProcessor();
        var store = new DataStore();
        var mesh = new Mesh([], [], [], [], []);
        var renderer = new MeshRenderer(mesh);
        int entity = store.Alloc(new MeshRendererComponent(renderer));

        processor.Process(store, 0f);
        store.Remove<MeshRendererComponent>(entity);

        processor.Process(store, 0f);
        Assert.False(renderer.IsDisposed, "The renderer must survive the tick that observes the removal.");

        processor.Process(store, 0f);
        Assert.True(renderer.IsDisposed);
        Assert.True(mesh.IsDisposed);
    }

    [Fact]
    public void FreedEntityRendererRetires()
    {
        var processor = new MeshRendererComponentProcessor();
        var store = new DataStore();
        var mesh = new Mesh([], [], [], [], []);
        var renderer = new MeshRenderer(mesh);
        int entity = store.Alloc(new MeshRendererComponent(renderer));

        processor.Process(store, 0f);
        store.Free(entity);

        processor.Process(store, 0f);
        processor.Process(store, 0f);

        Assert.True(renderer.IsDisposed);
        Assert.True(mesh.IsDisposed);
    }

    [Fact]
    public void ReplacedRendererWaitsForReplacementToBind()
    {
        var processor = new MeshRendererComponentProcessor();
        var store = new DataStore();
        var oldMesh = new Mesh([], [], [], [], []);
        var oldRenderer = new MeshRenderer(oldMesh);
        int entity = store.Alloc(new MeshRendererComponent(oldRenderer));
        processor.Process(store, 0f);

        var newMesh = new Mesh([], [], [], [], []);
        var newRenderer = new MeshRenderer(newMesh);
        store.AddOrUpdate(entity, new MeshRendererComponent(newRenderer));

        processor.Process(store, 0f);
        Assert.False(oldRenderer.IsDisposed, "The old renderer must wait for its replacement to bind.");

        newRenderer.IsBound = true;
        processor.Process(store, 0f);

        Assert.True(oldRenderer.IsDisposed);
        Assert.True(oldMesh.IsDisposed);
        Assert.False(newRenderer.IsDisposed);
        Assert.False(newMesh.IsDisposed);
    }

    [Fact]
    public void ReplacementNeverBoundRetiresAfterAgeThreshold()
    {
        var processor = new MeshRendererComponentProcessor();
        var store = new DataStore();
        var oldRenderer = new MeshRenderer(new Mesh([], [], [], [], []));
        int entity = store.Alloc(new MeshRendererComponent(oldRenderer));
        processor.Process(store, 0f);

        var newRenderer = new MeshRenderer(new Mesh([], [], [], [], []));
        store.AddOrUpdate(entity, new MeshRendererComponent(newRenderer));

        for (var i = 0; i < 4; i++)
        {
            processor.Process(store, 0f);
            Assert.False(oldRenderer.IsDisposed);
        }

        processor.Process(store, 0f);
        Assert.True(oldRenderer.IsDisposed);
        Assert.False(newRenderer.IsDisposed);
    }

    [Fact]
    public void RendererMovedToAnotherEntityIsNotRetired()
    {
        var processor = new MeshRendererComponentProcessor();
        var store = new DataStore();
        var renderer = new MeshRenderer(new Mesh([], [], [], [], []));
        int first = store.Alloc(new MeshRendererComponent(renderer));
        processor.Process(store, 0f);

        int second = store.Alloc();
        store.AddOrUpdate(second, new MeshRendererComponent(renderer));
        store.Remove<MeshRendererComponent>(first);

        processor.Process(store, 0f);
        processor.Process(store, 0f);
        Assert.False(renderer.IsDisposed, "A renderer that moved to a live entity must not be retired.");

        store.Remove<MeshRendererComponent>(second);
        processor.Process(store, 0f);
        processor.Process(store, 0f);
        Assert.True(renderer.IsDisposed);
    }

    [Fact]
    public void ReplacedRendererRevertedBeforeRetirementIsNotDisposed()
    {
        var processor = new MeshRendererComponentProcessor();
        var store = new DataStore();
        var first = new MeshRenderer(new Mesh([], [], [], [], []));
        var second = new MeshRenderer(new Mesh([], [], [], [], []));
        int entity = store.Alloc(new MeshRendererComponent(first));
        processor.Process(store, 0f);

        store.AddOrUpdate(entity, new MeshRendererComponent(second));
        processor.Process(store, 0f);

        //  Switch back to the first renderer before it retires.
        store.AddOrUpdate(entity, new MeshRendererComponent(first));
        first.IsBound = true;
        for (var i = 0; i < 6; i++)
        {
            processor.Process(store, 0f);
        }

        Assert.False(first.IsDisposed, "A renderer reverted back onto an entity before retirement must stay live.");
        Assert.True(second.IsDisposed);
    }

    [Fact]
    public void RendererMovedToLowerIndexEntityBeforeOldEntityIsReplacedIsNotDisposed()
    {
        var processor = new MeshRendererComponentProcessor();
        var store = new DataStore();
        var moved = new MeshRenderer(new Mesh([], [], [], [], []));
        var replacement = new MeshRenderer(new Mesh([], [], [], [], []));

        int lower = store.Alloc();
        int higher = store.Alloc(new MeshRendererComponent(moved));
        processor.Process(store, 0f);

        //  Move the renderer onto a lower-index entity and replace it on the higher-index entity.
        store.AddOrUpdate(lower, new MeshRendererComponent(moved));
        store.AddOrUpdate(higher, new MeshRendererComponent(replacement));
        replacement.IsBound = true;
        for (var i = 0; i < 6; i++)
        {
            processor.Process(store, 0f);
        }

        Assert.False(moved.IsDisposed, "A renderer moved to another live entity must not retire when its previous entity is replaced.");
        Assert.False(replacement.IsDisposed);
    }

    [Fact]
    public void NullRendererOnOldEntityKeepsRendererLiveOnNewEntity()
    {
        var processor = new MeshRendererComponentProcessor();
        var store = new DataStore();
        var renderer = new MeshRenderer(new Mesh([], [], [], [], []));
        int first = store.Alloc(new MeshRendererComponent(renderer));
        processor.Process(store, 0f);

        int second = store.Alloc(new MeshRendererComponent(renderer));

        //  Clearing the old component must not retire a renderer that is live on the new entity.
        store.AddOrUpdate(first, default(MeshRendererComponent));
        processor.Process(store, 0f);
        processor.Process(store, 0f);

        Assert.False(renderer.IsDisposed);
    }

    [Fact]
    public void NullRendererReplacementRetiresPreviousRenderer()
    {
        var processor = new MeshRendererComponentProcessor();
        var store = new DataStore();
        var renderer = new MeshRenderer(new Mesh([], [], [], [], []));
        int entity = store.Alloc(new MeshRendererComponent(renderer));
        processor.Process(store, 0f);

        store.AddOrUpdate(entity, default(MeshRendererComponent));
        processor.Process(store, 0f);
        processor.Process(store, 0f);

        Assert.True(renderer.IsDisposed);
    }

    [Fact]
    public void ReplacementThenFreeInSameTickRetiresBothRenderers()
    {
        var processor = new MeshRendererComponentProcessor();
        var store = new DataStore();
        var firstRenderer = new MeshRenderer(new Mesh([], [], [], [], []));
        int entity = store.Alloc(new MeshRendererComponent(firstRenderer));
        processor.Process(store, 0f);

        //  A replacement followed by a free in one tick captures only the live value. The
        //  tracked previous renderer must still retire.
        var secondRenderer = new MeshRenderer(new Mesh([], [], [], [], []));
        store.AddOrUpdate(entity, new MeshRendererComponent(secondRenderer));
        store.Free(entity);

        processor.Process(store, 0f);
        processor.Process(store, 0f);

        Assert.True(firstRenderer.IsDisposed);
        Assert.True(secondRenderer.IsDisposed);
    }

    [Fact]
    public void SharedMeshIsNotDisposedWhenRendererRetires()
    {
        var processor = new MeshRendererComponentProcessor();
        var store = new DataStore();
        var sharedMesh = new Mesh([], [], [], [], []) { Shared = true };

        var renderer = new MeshRenderer(sharedMesh);
        int entity = store.Alloc(new MeshRendererComponent(renderer));

        processor.Process(store, 0f);
        store.Remove<MeshRendererComponent>(entity);
        processor.Process(store, 0f);
        processor.Process(store, 0f);

        Assert.True(renderer.IsDisposed);
        Assert.False(sharedMesh.IsDisposed);
    }

    [Fact]
    public void ResourceLifetimeSystemResolvesRegisteredProcessorsAndDisposesRemoved()
    {
        using var container = new Container();
        container.Register<IComponentResourceProcessor, MeshRendererComponentProcessor>(Reuse.Singleton);
        container.Register<ResourceLifetimeSystem>(Reuse.Singleton);
        var system = container.Resolve<ResourceLifetimeSystem>();

        var store = new DataStore();
        var renderer = new MeshRenderer(new Mesh([], [], [], [], []));
        var batch = new MeshBatch();
        var batchRenderer = new MeshBatchRenderer(batch);
        int entity = store.Alloc(new MeshRendererComponent(renderer), new MeshBatchRendererComponent(batchRenderer));
        store.Free(entity);

        system.Tick(0f, store);
        system.Tick(0f, store);

        Assert.True(renderer.IsDisposed);
        Assert.True(batchRenderer.IsDisposed);
        Assert.True(batch.IsDisposed);
    }

    [Fact]
    public void RemovedBatchRendererReleasesRendererAndBatch()
    {
        var store = new DataStore();
        var batch = new MeshBatch();
        var renderer = new MeshBatchRenderer(batch);
        int entity = store.Alloc(new MeshBatchRendererComponent(renderer));

        store.Remove<MeshBatchRendererComponent>(entity);
        store.DisposeRemoved();

        Assert.True(renderer.IsDisposed);
        Assert.True(batch.IsDisposed);
    }

    [Fact]
    public void RemovedPhysicsComponentDestroysBody()
    {
        var physics = new JoltPhysicsSystem(NullLogger.Instance, new PhysicsSettings());
        physics.SetGravity(Vector3.Zero);
        var store = new DataStore();
        int entity = CreateBody(store);

        physics.Tick(physics.FixedTimestamp * 1.25f, store);
        Assert.Equal(1u, physics.System.BodiesCount);

        store.Remove<PhysicsComponent>(entity);
        physics.Tick(physics.FixedTimestamp * 1.25f, store);

        Assert.Equal(0u, physics.System.BodiesCount);
    }

    [Fact]
    public void FreedPhysicsBodyIsDestroyedExactlyOnce()
    {
        var physics = new JoltPhysicsSystem(NullLogger.Instance, new PhysicsSettings());
        physics.SetGravity(Vector3.Zero);
        var store = new DataStore();
        int entity = CreateBody(store);

        physics.Tick(physics.FixedTimestamp * 1.25f, store);
        Assert.Equal(1u, physics.System.BodiesCount);

        store.Free(entity);
        physics.Tick(physics.FixedTimestamp * 1.25f, store);
        Assert.Equal(0u, physics.System.BodiesCount);

        physics.Tick(physics.FixedTimestamp * 1.25f, store);
        Assert.Equal(0u, physics.System.BodiesCount);
    }

    [Fact]
    public void DisposingAComponentCopyBeforeTheRemovalIsSafe()
    {
        var physics = new JoltPhysicsSystem(NullLogger.Instance, new PhysicsSettings());
        physics.SetGravity(Vector3.Zero);
        var store = new DataStore();
        int entity = CreateBody(store);

        physics.Tick(physics.FixedTimestamp * 1.25f, store);
        Assert.Equal(1u, physics.System.BodiesCount);

        //  A caller disposes a value copy, then the entity is freed. The captured copy releases
        //  the same body again, and the second release must be a no-op.
        Assert.True(store.TryGet(entity, out PhysicsComponent copy));
        copy.Dispose();
        store.Free(entity);

        physics.Tick(physics.FixedTimestamp * 1.25f, store);
        physics.Tick(physics.FixedTimestamp * 1.25f, store);

        Assert.Equal(0u, physics.System.BodiesCount);
    }

    [Fact]
    public void RecycledEntityIndexStillDestroysPreviousBody()
    {
        var physics = new JoltPhysicsSystem(NullLogger.Instance, new PhysicsSettings());
        physics.SetGravity(Vector3.Zero);
        var store = new DataStore();
        int entity = CreateBody(store);

        physics.Tick(physics.FixedTimestamp * 1.25f, store);
        Assert.Equal(1u, physics.System.BodiesCount);

        //  A freed index is reused immediately, as the server does on a same-tick rejoin.
        store.Free(entity);
        int recycled = store.Alloc();
        Assert.Equal(entity, recycled);
        store.AddOrUpdate(recycled, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(recycled, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Discrete));
        store.AddOrUpdate(recycled, new ColliderComponent(new Box3(Vector3.One)));

        physics.Tick(physics.FixedTimestamp * 1.25f, store);

        //  The previous body is gone and the new one exists.
        Assert.Equal(1u, physics.System.BodiesCount);
    }

    private static int CreateBody(DataStore store)
    {
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(entity, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Discrete));
        store.AddOrUpdate(entity, new ColliderComponent(new Box3(Vector3.One)));
        return entity;
    }
}
