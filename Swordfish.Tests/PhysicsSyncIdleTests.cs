using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Types.Shapes;
using Swordfish.Physics;
using Swordfish.Physics.Jolt;
using Swordfish.Settings;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Pins the idle path of the Jolt sync. An unchanged body must not re-dirty its replicated
/// components, and it must be allowed to sleep instead of having its sleep timer reset every step.
/// </summary>
public class PhysicsSyncIdleTests
{
    [Fact]
    public void IdleBodySleepsWithoutDirtyingReplicatedComponents()
    {
        var settings = new PhysicsSettings();
        var physics = new JoltPhysicsSystem(NullLogger.Instance, settings);
        physics.SetGravity(Vector3.Zero);

        var store = new DataStore();
        int entity = CreateBody(store);

        float delta = physics.FixedTimestamp * 1.25f;
        for (var i = 0; i < 120; i++)
        {
            physics.Tick(delta, store);
        }

        Assert.True(store.TryGet(entity, out PhysicsComponent settled));
        Assert.NotNull(settled.Body);
        Assert.False(settled.Body!.IsActive, "An unchanged body must be allowed to sleep.");

        store.ClearDirty<TransformComponent>(entity);
        store.ClearDirty<PhysicsComponent>(entity);

        for (var i = 0; i < 10; i++)
        {
            physics.Tick(delta, store);
        }

        Assert.False(store.IsDirty<TransformComponent>(entity), "An idle body must not re-dirty its transform.");
        Assert.False(store.IsDirty<PhysicsComponent>(entity), "An idle body must not re-dirty its physics.");
    }

    [Fact]
    public void ChangedVelocityWakesAndDirties()
    {
        var settings = new PhysicsSettings();
        var physics = new JoltPhysicsSystem(NullLogger.Instance, settings);
        physics.SetGravity(Vector3.Zero);

        var store = new DataStore();
        int entity = CreateBody(store);

        float delta = physics.FixedTimestamp * 1.25f;
        for (var i = 0; i < 120; i++)
        {
            physics.Tick(delta, store);
        }

        store.QueryRef(entity, 0f, (float _, DataStore _, int e, ref Ref<PhysicsComponent> physicsComponent) =>
        {
            physicsComponent.Write.Velocity = new Vector3(1f, 0f, 0f);
        });
        store.ClearDirty<TransformComponent>(entity);
        store.ClearDirty<PhysicsComponent>(entity);

        physics.Tick(delta, store);

        Assert.True(store.IsDirty<PhysicsComponent>(entity), "A changed velocity must be replicated.");
        Assert.True(store.IsDirty<TransformComponent>(entity), "A moved body must replicate its transform.");
        Assert.True(store.TryGet(entity, out TransformComponent moved) && moved.Position.X > 0f);
    }

    [Fact]
    public void SettledBodyOnFloorSleepsWithoutDirtying()
    {
        var settings = new PhysicsSettings();
        var physics = new JoltPhysicsSystem(NullLogger.Instance, settings);
        var store = new DataStore();

        int floor = store.Alloc();
        store.AddOrUpdate(floor, new TransformComponent(new Vector3(0f, -0.5f, 0f), Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(floor, new PhysicsComponent(Layers.NON_MOVING, BodyType.Static, CollisionDetection.Discrete));
        store.AddOrUpdate(floor, new ColliderComponent(new Box3(new Vector3(20f, 1f, 20f))));

        int box = store.Alloc();
        store.AddOrUpdate(box, new TransformComponent(new Vector3(0f, 2f, 0f), Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(box, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Discrete));
        store.AddOrUpdate(box, new ColliderComponent(new Box3(Vector3.One)));

        float delta = physics.FixedTimestamp * 1.25f;
        for (var i = 0; i < 240; i++)
        {
            physics.Tick(delta, store);
        }

        Assert.True(store.TryGet(box, out PhysicsComponent settled));
        Assert.NotNull(settled.Body);
        Assert.False(settled.Body!.IsActive, "A box that rests on the floor must be allowed to sleep.");

        store.ClearDirty<TransformComponent>(box);
        store.ClearDirty<PhysicsComponent>(box);

        for (var i = 0; i < 10; i++)
        {
            physics.Tick(delta, store);
        }

        Assert.False(store.IsDirty<TransformComponent>(box), "A settled body must not re-dirty its transform.");
        Assert.False(store.IsDirty<PhysicsComponent>(box), "A settled body must not re-dirty its physics.");
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
