using System;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Types;
using Swordfish.Library.Types.Shapes;
using Swordfish.Physics;
using Swordfish.Physics.Jolt;
using Swordfish.Settings;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Pins <see cref="ConvexHull"/> and <see cref="CompoundShape"/> integration in <see cref="JoltPhysicsSystem"/>.
/// </summary>
public class ConvexHullPhysicsTests
{
    private static readonly ConvexHull SlopeHull = new([
        new Vector3(0.5f, 0.5f, -0.5f),
        new Vector3(0.5f, -0.5f, 0.5f),
        new Vector3(0.5f, -0.5f, -0.5f),
        new Vector3(-0.5f, 0.5f, -0.5f),
        new Vector3(-0.5f, -0.5f, 0.5f),
        new Vector3(-0.5f, -0.5f, -0.5f),
    ]);

    [Fact]
    public void RaycastAgainstSlopeHullHitsDiagonalFaceAndMissesEmptyCorner()
    {
        var physics = new JoltPhysicsSystem(NullLogger.Instance, new PhysicsSettings());
        physics.SetGravity(Vector3.Zero);
        var store = new DataStore();

        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(entity, new PhysicsComponent(Layers.NON_MOVING, BodyType.Static, CollisionDetection.Discrete));
        store.AddOrUpdate(entity, new ColliderComponent(new CompoundShape(
            [SlopeHull],
            [Vector3.Zero],
            [Quaternion.Identity]
        )));

        physics.Tick(physics.FixedTimestamp * 1.25f, store);

        //  Ray straight down through the center hits the 45-degree sloped surface at y = 0.
        RaycastResult hit = physics.Raycast(new Ray(new Vector3(0f, 2f, 0f), new Vector3(0f, -4f, 0f)));
        Assert.True(hit.Hit, "Ray into the sloped face must hit.");
        Assert.Equal(entity, hit.Entity.Ptr);

        float expectedComponent = MathF.Sqrt(0.5f);
        Assert.InRange(hit.Normal.Y, expectedComponent - 0.05f, expectedComponent + 0.05f);
        Assert.InRange(hit.Normal.Z, expectedComponent - 0.05f, expectedComponent + 0.05f);

        //  Ray through the empty upper-front corner (+Y, +Z) must miss the wedge.
        RaycastResult miss = physics.Raycast(new Ray(new Vector3(-2f, 0.35f, 0.35f), new Vector3(4f, 0f, 0f)));
        Assert.False(miss.Hit, "Ray through the empty upper-front half of a slope must miss.");
    }

    [Fact]
    public void DynamicCompoundWithBoxAndSlopeHullSimulatesCleanly()
    {
        var physics = new JoltPhysicsSystem(NullLogger.Instance, new PhysicsSettings());
        var store = new DataStore();

        int floor = store.Alloc();
        store.AddOrUpdate(floor, new TransformComponent(new Vector3(0f, -0.5f, 0f), Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(floor, new PhysicsComponent(Layers.NON_MOVING, BodyType.Static, CollisionDetection.Discrete));
        store.AddOrUpdate(floor, new ColliderComponent(new Box3(new Vector3(20f, 1f, 20f))));

        int structure = store.Alloc();
        store.AddOrUpdate(structure, new TransformComponent(new Vector3(0f, 2f, 0f), Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(structure, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous));
        store.AddOrUpdate(structure, new ColliderComponent(new CompoundShape(
            [new Box3(Vector3.One), SlopeHull],
            [Vector3.Zero, new Vector3(0f, 1f, 0f)],
            [Quaternion.Identity, Quaternion.Identity]
        )));

        float delta = physics.FixedTimestamp * 1.25f;
        for (var i = 0; i < 60; i++)
        {
            physics.Tick(delta, store);
        }

        Assert.True(store.TryGet(structure, out PhysicsComponent physicsComponent));
        Assert.NotNull(physicsComponent.Body);
    }
}
