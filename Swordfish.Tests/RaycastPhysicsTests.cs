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

/// <summary>Pins <see cref="IPhysics.Raycast(in Ray, int)"/> body filtering in <see cref="JoltPhysicsSystem"/>.</summary>
public sealed class RaycastPhysicsTests
{
    [Fact]
    public void RaycastWithIgnoreEntitySkipsSelfColliderWhenStartingInsideBodyAndResetsFilter()
    {
        var physics = new JoltPhysicsSystem(NullLogger.Instance, new PhysicsSettings());
        physics.SetGravity(Vector3.Zero);
        var store = new DataStore();

        //  Player box centered vertically at -0.2 below body origin, enclosing rays cast downward from origin.
        int player = store.Alloc();
        store.AddOrUpdate(player, new TransformComponent(new Vector3(0f, 1f, 0f), Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(player, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous));
        var box = new Shape(new Box3(new Vector3(0.25f, 0.75f, 0.25f)));
        var compound = new CompoundShape([box], [new Vector3(0f, -0.2f, 0f)], [Quaternion.Identity]);
        store.AddOrUpdate(player, new ColliderComponent(compound));

        //  Static floor underneath the player.
        int floor = store.Alloc();
        store.AddOrUpdate(floor, new TransformComponent(new Vector3(0f, -1f, 0f), Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(floor, new PhysicsComponent(Layers.NON_MOVING, BodyType.Static, CollisionDetection.Discrete));
        store.AddOrUpdate(floor, new ColliderComponent(new Box3(new Vector3(4f, 1f, 4f))));

        physics.Tick(physics.FixedTimestamp * 1.25f, store);

        var downwardRay = new Ray(new Vector3(0f, 1f, 0f), new Vector3(0f, -9.5f, 0f));

        //  Unfiltered ray starting inside the player box hits the player collider itself.
        RaycastResult unfiltered = physics.Raycast(in downwardRay);
        Assert.True(unfiltered.Hit);
        Assert.Equal(player, unfiltered.Entity.Ptr);

        //  Ignoring the player skips the enclosing body and hits the floor underneath.
        RaycastResult filtered = physics.Raycast(in downwardRay, ignoreEntity: player);
        Assert.True(filtered.Hit);
        Assert.Equal(floor, filtered.Entity.Ptr);
        Assert.Equal(-0.5f, filtered.Point.Y, precision: 3);
        Assert.Equal(Vector3.UnitY, filtered.Normal);

        //  A follow-up unfiltered ray must hit the player again, proving IgnoredBodyID was cleared.
        RaycastResult subsequent = physics.Raycast(in downwardRay);
        Assert.True(subsequent.Hit);
        Assert.Equal(player, subsequent.Entity.Ptr);
    }
}
