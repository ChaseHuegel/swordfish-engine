using System;
using System.Collections.Generic;
using System.Numerics;
using JoltPhysicsSharp;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Types.Shapes;
using Swordfish.Physics;
using Swordfish.Physics.Jolt;
using Xunit;
using BodyType = Swordfish.Physics.BodyType;
using PhysicsSettings = Swordfish.Settings.PhysicsSettings;
using Ray = Swordfish.Physics.Ray;

namespace Swordfish.Tests;

/// <summary>Pins Jolt contact listener event capture, thread safety, and allocation behavior in <see cref="JoltPhysicsSystem"/>.</summary>
public sealed class ContactPhysicsTests
{
    [Fact]
    public void DynamicBodyHittingStaticFloorEmitsAddedContactWithPreImpulseSpeedAndNoSpam()
    {
        var (physics, store) = CreateWorld();

        //  Floor top surface sits at y = 0.
        int floor = AddBox(store, new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f), BodyType.Static);
        //  1m cube starts with bottom at y = 0.25m, moving downward at 10 m/s.
        int box = AddBox(store, new Vector3(0f, 0.75f, 0f), Vector3.One, BodyType.Dynamic, new Vector3(0f, -10f, 0f), CollisionDetection.Continuous);

        var events = new List<PhysicsContactEvent>();
        physics.ContactResolved += events.Add;

        float stepDelta = physics.FixedTimestamp * 1.05f;
        for (var i = 0; i < 30; i++)
        {
            physics.Tick(stepDelta, store);
        }

        Assert.Single(events);

        PhysicsContactEvent contact = events[0];
        Assert.Equal(PhysicsContactKind.Added, contact.Kind);
        Assert.True(
            (contact.EntityA == floor && contact.EntityB == box)
            || (contact.EntityA == box && contact.EntityB == floor),
            $"Expected contact pair ({floor}, {box}), got ({contact.EntityA}, {contact.EntityB})."
        );

        float expectedNormalY = contact.EntityA == floor ? 1f : -1f;
        Assert.InRange(contact.Normal.Y, expectedNormalY - 0.05f, expectedNormalY + 0.05f);
        Assert.InRange(MathF.Abs(contact.Normal.X), 0f, 0.05f);
        Assert.InRange(MathF.Abs(contact.Normal.Z), 0f, 0.05f);

        Assert.InRange(contact.ContactPoint.X, -0.1f, 0.1f);
        Assert.InRange(contact.ContactPoint.Y, -0.1f, 0.1f);
        Assert.InRange(contact.ContactPoint.Z, -0.1f, 0.1f);

        Assert.InRange(contact.ImpactSpeed, 9.5f, 10.5f);
        Assert.Equal(contact.ImpactSpeed, Vector3.Dot(contact.RelativeVelocity, contact.Normal), precision: 3);

        //  Resting contact on subsequent ticks must not emit Added events repeatedly.
        events.Clear();
        for (var i = 0; i < 20; i++)
        {
            physics.Tick(stepDelta, store);
        }

        Assert.Empty(events);
    }

    [Fact]
    public void DynamicBodyToBodyCollisionReportsAccurateEntitiesNormalPointAndClosingSpeed()
    {
        var (physics, store) = CreateWorld();

        int bodyA = AddBox(store, new Vector3(-1.5f, 0f, 0f), Vector3.One, BodyType.Dynamic, new Vector3(6f, 0f, 0f), CollisionDetection.Continuous);
        int bodyB = AddBox(store, new Vector3(1.5f, 0f, 0f), Vector3.One, BodyType.Dynamic, new Vector3(-4f, 0f, 0f), CollisionDetection.Continuous);

        var events = new List<PhysicsContactEvent>();
        physics.ContactResolved += events.Add;

        float stepDelta = physics.FixedTimestamp * 1.05f;
        for (var i = 0; i < 30; i++)
        {
            physics.Tick(stepDelta, store);
        }

        Assert.Single(events);

        PhysicsContactEvent contact = events[0];
        Assert.Equal(PhysicsContactKind.Added, contact.Kind);
        Assert.True(
            (contact.EntityA == bodyA && contact.EntityB == bodyB)
            || (contact.EntityA == bodyB && contact.EntityB == bodyA),
            $"Expected contact pair ({bodyA}, {bodyB}), got ({contact.EntityA}, {contact.EntityB})."
        );

        float expectedNormalX = contact.EntityA == bodyA ? 1f : -1f;
        Assert.InRange(contact.Normal.X, expectedNormalX - 0.05f, expectedNormalX + 0.05f);
        Assert.InRange(MathF.Abs(contact.Normal.Y), 0f, 0.05f);
        Assert.InRange(MathF.Abs(contact.Normal.Z), 0f, 0.05f);

        //  Faces meet near x = 0.2m with 10 m/s total closing speed.
        Assert.InRange(contact.ContactPoint.X, -0.05f, 0.45f);
        Assert.InRange(MathF.Abs(contact.ContactPoint.Y), 0f, 0.05f);
        Assert.InRange(MathF.Abs(contact.ContactPoint.Z), 0f, 0.05f);

        Assert.InRange(contact.ImpactSpeed, 9.5f, 10.5f);
        Assert.Equal(contact.ImpactSpeed, Vector3.Dot(contact.RelativeVelocity, contact.Normal), precision: 3);
    }

    [Fact]
    public void FreedRemovedAndUnmappedBodiesEmitNoInvalidCallbacks()
    {
        var (physics, store) = CreateWorld();

        int mover = AddBox(store, new Vector3(-2f, 0f, 0f), Vector3.One, BodyType.Dynamic, new Vector3(10f, 0f, 0f), CollisionDetection.Continuous);
        int freedTarget = AddBox(store, new Vector3(0f, 0f, 0f), Vector3.One, BodyType.Static);
        int removedTarget = AddBox(store, new Vector3(2f, 0f, 0f), Vector3.One, BodyType.Static);

        //  Create a raw Jolt body without entity UserData (default 0) at x = 4.
        using var rawShape = new BoxShape(new Vector3(0.5f, 0.5f, 0.5f));
        using var rawSettings = new BodyCreationSettings(rawShape, new Vector3(4f, 0f, 0f), Quaternion.Identity, MotionType.Static, Layers.NON_MOVING);
        BodyID rawBodyId = physics.System.BodyInterface.CreateAndAddBody(rawSettings, Activation.DontActivate);

        var events = new List<PhysicsContactEvent>();
        physics.ContactResolved += events.Add;

        //  Warm up tick 1 to create all entity-backed bodies.
        float stepDelta = physics.FixedTimestamp * 1.05f;
        physics.Tick(stepDelta, store);

        //  Free one entity and remove PhysicsComponent from another before mover reaches them.
        store.Free(freedTarget);
        store.Remove<PhysicsComponent>(removedTarget);

        for (var i = 0; i < 60; i++)
        {
            physics.Tick(stepDelta, store);
        }

        Assert.Empty(events);
        Assert.True(mover >= 0);

        physics.System.BodyInterface.RemoveAndDestroyBody(rawBodyId);
    }

    [Fact]
    public void ContactCallbacksRunOnTickThreadOutsideSolverLockAndSupportReentry()
    {
        var (physics, store) = CreateWorld();
        int tickThreadId = Environment.CurrentManagedThreadId;

        int floor = AddBox(store, new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f), BodyType.Static);
        int box = AddBox(store, new Vector3(0f, 0.75f, 0f), Vector3.One, BodyType.Dynamic, new Vector3(0f, -10f, 0f), CollisionDetection.Continuous);

        int callbackThreadId = -1;
        RaycastResult raycastInsideCallback = default;
        var overlapInsideCallback = false;
        var callbackCount = 0;

        physics.ContactResolved += contact =>
        {
            callbackCount++;
            callbackThreadId = Environment.CurrentManagedThreadId;
            raycastInsideCallback = physics.Raycast(new Ray(new Vector3(0f, 5f, 0f), new Vector3(0f, -10f, 0f)), ignoreEntity: box);
            overlapInsideCallback = physics.Overlaps(new ShapeOverlapQuery(new Box3(Vector3.One), new Vector3(0f, -0.5f, 0f), Quaternion.Identity, Vector3.One, ignoredEntity: box));

            //  Freeing the dynamic body mid-tick must clean up its body before subsequent accumulated steps run.
            store.Free(box);
        };

        //  Tick 1 creates the bodies; tick 2 runs multiple fixed steps spanning impact and post-free steps.
        physics.Tick(physics.FixedTimestamp * 1.05f, store);
        physics.Tick(physics.FixedTimestamp * 4.5f, store);

        Assert.Equal(1, callbackCount);
        Assert.Equal(tickThreadId, callbackThreadId);
        Assert.True(raycastInsideCallback.Hit);
        Assert.Equal(floor, raycastInsideCallback.Entity.Ptr);
        Assert.True(overlapInsideCallback);
        Assert.Equal(1u, physics.System.BodiesCount);
    }

    [Fact]
    public void NonCollidingBodiesEmitNoContactsAndAllocateZeroBytesInSteadyState()
    {
        var (physics, store) = CreateWorld();

        AddBox(store, new Vector3(-10f, 0f, 0f), Vector3.One, BodyType.Dynamic);
        AddBox(store, new Vector3(10f, 0f, 0f), Vector3.One, BodyType.Dynamic);

        var contactCount = 0;
        physics.ContactResolved += _ => contactCount++;

        float stepDelta = physics.FixedTimestamp * 1.05f;
        for (var i = 0; i < 4; i++)
        {
            physics.Tick(stepDelta, store);
        }

        //  Active non-colliding bodies allocate zero bytes per tick before sleep.
        long activeBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 15; i++)
        {
            physics.Tick(stepDelta, store);
        }

        long activeAfter = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(0L, activeAfter - activeBefore);

        //  Advance past Jolt's sleep threshold so sleep-transition callbacks are warmed up, then verify zero steady-state allocations.
        for (var i = 0; i < 45; i++)
        {
            physics.Tick(stepDelta, store);
        }

        long settledBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 60; i++)
        {
            physics.Tick(stepDelta, store);
        }

        long settledAfter = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, contactCount);
        Assert.Equal(0L, settledAfter - settledBefore);
    }

    [Fact]
    public void HighContactCountExceedingInitialBufferCapacityDeliversAllContacts()
    {
        var (physics, store) = CreateWorld();

        int floor = AddBox(store, new Vector3(0f, -0.5f, 0f), new Vector3(200f, 1f, 200f), BodyType.Static);

        //  Spawn 300 spaced dynamic boxes above the floor so all exceed the 256-slot fast buffer on impact.
        const int boxCount = 300;
        var expectedBoxes = new HashSet<int>(boxCount);
        for (var i = 0; i < boxCount; i++)
        {
            int row = i / 20;
            int col = i % 20;
            var position = new Vector3((col - 10) * 3f, 0.75f, (row - 10) * 3f);
            expectedBoxes.Add(AddBox(store, position, Vector3.One, BodyType.Dynamic, new Vector3(0f, -10f, 0f), CollisionDetection.Continuous));
        }

        var contactedBoxes = new HashSet<int>(boxCount);
        physics.ContactResolved += contact =>
        {
            int dynamicEntity = contact.EntityA == floor ? contact.EntityB : contact.EntityA;
            contactedBoxes.Add(dynamicEntity);
        };

        float stepDelta = physics.FixedTimestamp * 1.05f;
        for (var i = 0; i < 20; i++)
        {
            physics.Tick(stepDelta, store);
        }

        Assert.Equal(boxCount, contactedBoxes.Count);
        Assert.True(expectedBoxes.SetEquals(contactedBoxes));
    }

    [Fact]
    public void InterfaceDefaultMemberAllowsExistingIPhysicsStubsWithoutContactImplementation()
    {
        IPhysics stub = new MinimalPhysicsStub();
        var invoked = false;
        Action<PhysicsContactEvent> handler = _ => invoked = true;

        stub.ContactResolved += handler;
        stub.ContactResolved -= handler;

        Assert.False(invoked);
    }

    private static (JoltPhysicsSystem Physics, DataStore Store) CreateWorld()
    {
        var physics = new JoltPhysicsSystem(NullLogger.Instance, new PhysicsSettings());
        physics.SetGravity(Vector3.Zero);
        return (physics, new DataStore());
    }

    private static int AddBox(
        DataStore store,
        Vector3 position,
        Vector3 extents,
        BodyType bodyType,
        Vector3 velocity = default,
        CollisionDetection collisionDetection = CollisionDetection.Discrete
    )
    {
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TransformComponent(position, Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(entity, new PhysicsComponent(bodyType == BodyType.Static ? Layers.NON_MOVING : Layers.MOVING, bodyType, collisionDetection, velocity, Vector3.Zero));
        store.AddOrUpdate(entity, new ColliderComponent(new Box3(extents)));
        return entity;
    }

    private sealed class MinimalPhysicsStub : IPhysics
    {
        public float FixedTimestamp => 1f / 64f;

        public event EventHandler<EventArgs>? FixedUpdate
        {
            add { }
            remove { }
        }

        public RaycastResult Raycast(in Ray ray) => default;

        public bool Overlaps(in ShapeOverlapQuery query) => false;

        public void SetGravity(Vector3 gravity) { }
    }
}
