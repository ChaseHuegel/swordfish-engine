using System;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Types.Shapes;
using Swordfish.Physics;
using Swordfish.Physics.Jolt;
using Swordfish.Settings;
using Xunit;

namespace Swordfish.Tests;

/// <summary>Pins <see cref="IPhysics.Overlaps"/> in <see cref="JoltPhysicsSystem"/>.</summary>
public class ShapeOverlapPhysicsTests
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
    public void DetectsBoxOverlapAndClearsWhenSeparated()
    {
        var (physics, store) = CreateWorld();

        int body = AddBox(store, new Vector3(0f, 0f, 0f), Vector3.One, BodyType.Dynamic);
        physics.Tick(physics.FixedTimestamp * 1.25f, store);

        var overlapping = new ShapeOverlapQuery(new Box3(Vector3.One), new Vector3(0.4f, 0f, 0f), Quaternion.Identity, Vector3.One);
        var separated = new ShapeOverlapQuery(new Box3(Vector3.One), new Vector3(2f, 0f, 0f), Quaternion.Identity, Vector3.One);

        Assert.True(physics.Overlaps(in overlapping), "Penetrating box must report overlap.");
        Assert.False(physics.Overlaps(in separated), "Separated box must not report overlap.");
        Assert.True(body >= 0);
    }

    [Fact]
    public void IgnoredEntityIsExcludedWhileOtherBodiesStillCollide()
    {
        var (physics, store) = CreateWorld();

        int ignored = AddBox(store, Vector3.Zero, Vector3.One, BodyType.Static);
        int other = AddBox(store, new Vector3(3f, 0f, 0f), Vector3.One, BodyType.Dynamic);
        physics.Tick(physics.FixedTimestamp * 1.25f, store);

        var onlyIgnored = new ShapeOverlapQuery(new Box3(Vector3.One), Vector3.Zero, Quaternion.Identity, Vector3.One, ignoredEntity: ignored);
        var hitsOther = new ShapeOverlapQuery(new Box3(Vector3.One), new Vector3(3f, 0f, 0f), Quaternion.Identity, Vector3.One, ignoredEntity: ignored);

        Assert.False(physics.Overlaps(in onlyIgnored), "Ignored entity must not trigger overlap.");
        Assert.True(physics.Overlaps(in hitsOther), "Non-ignored entity must still trigger overlap.");
        Assert.True(other >= 0);
    }

    [Fact]
    public void FlushTouchingFacesAreIgnoredWhenMinPenetrationIsSet()
    {
        var (physics, store) = CreateWorld();

        AddBox(store, Vector3.Zero, Vector3.One, BodyType.Static);
        physics.Tick(physics.FixedTimestamp * 1.25f, store);

        //  Faces touch at x = 0.5 with zero penetration.
        var flush = new ShapeOverlapQuery(new Box3(Vector3.One), new Vector3(1f, 0f, 0f), Quaternion.Identity, Vector3.One, minPenetration: 0.005f);
        //  Penetrates by 0.05 m along X.
        var penetrating = new ShapeOverlapQuery(new Box3(Vector3.One), new Vector3(0.95f, 0f, 0f), Quaternion.Identity, Vector3.One, minPenetration: 0.005f);

        Assert.False(physics.Overlaps(in flush), "Flush touching boxes must not report overlap beyond minPenetration.");
        Assert.True(physics.Overlaps(in penetrating), "Penetrating boxes must report overlap beyond minPenetration.");
    }

    [Fact]
    public void ConvexHullOverlapRespectsCenterOfMassAndOrientation()
    {
        var (physics, store) = CreateWorld();

        //  Small dynamic body sitting in the empty upper-front corner (+Y, +Z) of a slope at the origin.
        AddBox(store, new Vector3(0f, 0.35f, 0.35f), new Vector3(0.2f, 0.2f, 0.2f), BodyType.Dynamic);
        physics.Tick(physics.FixedTimestamp * 1.25f, store);

        var unrotatedSlope = new ShapeOverlapQuery(SlopeHull, Vector3.Zero, Quaternion.Identity, Vector3.One, minPenetration: 0.005f);
        //  Rotating 180 degrees around Y moves the solid high back (-Z) to +Z, overlapping the body.
        var rotatedSlope = new ShapeOverlapQuery(SlopeHull, Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI), Vector3.One, minPenetration: 0.005f);

        Assert.False(physics.Overlaps(in unrotatedSlope), "Body in the empty corner of a slope must not overlap.");
        Assert.True(physics.Overlaps(in rotatedSlope), "Rotating the solid half of the slope into the body must overlap.");
    }

    [Fact]
    public void DetectsNonBoxCollidersSuchAsCapsules()
    {
        var (physics, store) = CreateWorld();

        int npc = store.Alloc();
        store.AddOrUpdate(npc, new TransformComponent(new Vector3(0f, 1f, 0f), Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(npc, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous));
        store.AddOrUpdate(npc, new ColliderComponent(new Capsule(height: 1.0f, radius: 0.35f)));
        physics.Tick(physics.FixedTimestamp * 1.25f, store);

        var overlapping = new ShapeOverlapQuery(new Box3(Vector3.One), new Vector3(0f, 1f, 0f), Quaternion.Identity, Vector3.One, minPenetration: 0.005f);
        var clear = new ShapeOverlapQuery(new Box3(Vector3.One), new Vector3(2f, 1f, 0f), Quaternion.Identity, Vector3.One, minPenetration: 0.005f);

        Assert.True(physics.Overlaps(in overlapping), "Box overlapping a capsule collider must report overlap.");
        Assert.False(physics.Overlaps(in clear), "Box separated from a capsule collider must not report overlap.");
    }

    private static (JoltPhysicsSystem Physics, DataStore Store) CreateWorld()
    {
        var physics = new JoltPhysicsSystem(NullLogger.Instance, new PhysicsSettings());
        physics.SetGravity(Vector3.Zero);
        return (physics, new DataStore());
    }

    private static int AddBox(DataStore store, Vector3 position, Vector3 extents, BodyType bodyType)
    {
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TransformComponent(position, Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(entity, new PhysicsComponent(bodyType == BodyType.Static ? Layers.NON_MOVING : Layers.MOVING, bodyType, CollisionDetection.Discrete));
        store.AddOrUpdate(entity, new ColliderComponent(new Box3(extents)));
        return entity;
    }
}
