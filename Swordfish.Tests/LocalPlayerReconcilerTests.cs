#nullable enable
using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Util;
using Swordfish.Physics;
using Xunit;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Networking;

namespace Swordfish.Tests;

/// <summary>
/// Pins the local-player orientation policy: look stays client-predicted, collision rotation is
/// reconciled toward the server target, and the server angular velocity applies only while look is idle.
/// </summary>
public class LocalPlayerReconcilerTests
{
    [Fact]
    public void PositionCorrectionBlendsAndConverges()
    {
        TestContext context = CreateContext();
        var first = new Vector3(4f, 5f, 6f);
        var second = new Vector3(6f, 5f, 6f);

        ApplyTransform(context, first, Quaternion.Identity);
        ApplyTransform(context, second, Quaternion.Identity);

        Assert.True(context.Store.TryGet(context.Player, out TransformComponent transform));
        Assert.Equal(5f, transform.Position.X, 3);

        ApplyTransform(context, second, Quaternion.Identity);
        Assert.True(context.Store.TryGet(context.Player, out transform));
        Assert.Equal(5.5f, transform.Position.X, 3);
    }

    [Fact]
    public void PositionCorrectionIgnoresSubCentimeterError()
    {
        TestContext context = CreateContext();
        var position = new Vector3(4f, 5f, 6f);
        var withinDeadband = position + new Vector3(0.005f, 0f, 0f);

        ApplyTransform(context, position, Quaternion.Identity);
        ApplyTransform(context, withinDeadband, Quaternion.Identity);

        Assert.True(context.Store.TryGet(context.Player, out TransformComponent transform));
        Assert.Equal(position, transform.Position);
    }

    [Fact]
    public void OrientationConvergesToServerOrientation()
    {
        TestContext context = CreateContext();
        var serverOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1.5f);

        ApplyTransform(context, context.ServerPosition, serverOrientation);
        ApplySnapshot(context, 0);
        Tick(context, 300);

        Assert.True(context.Store.TryGet(context.Player, out TransformComponent transform));
        Assert.Equal(context.ServerPosition, transform.Position);
        Assert.Equal(Vector3.One, transform.Scale);
        Assert.True(Quaternion.Dot(transform.Orientation, serverOrientation) > 0.9999f);
    }

    [Fact]
    public void OrientationTargetIncludesPendingLook()
    {
        TestContext context = CreateContext();
        var serverOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1.5f);
        var pendingLook = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f);
        Quaternion target = Quaternion.Normalize(serverOrientation * pendingLook);

        SetInput(context, lookYaw: 0.5f);
        ApplyTransform(context, context.ServerPosition, serverOrientation);
        ApplySnapshot(context, 0);
        Tick(context, 300);

        Assert.True(context.Store.TryGet(context.Player, out TransformComponent transform));
        Assert.True(Quaternion.Dot(transform.Orientation, target) > 0.9999f);
        Assert.True(Quaternion.Dot(transform.Orientation, serverOrientation) < 0.9999f);
    }

    [Fact]
    public void AckedLookSurvivesRingTrim()
    {
        TestContext context = CreateContext();
        var serverOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1.5f);

        SetInput(context, lookYaw: 0.5f);
        AddPendingInput(context, lookYaw: 0.5f, serverTickAtSample: 0);
        ApplyTransform(context, context.ServerPosition, serverOrientation);
        ApplySnapshot(context, 0);
        TrimPendingInput(context, 0);

        //  The ack stays at zero, but the matching sample was trimmed. The known acked totals must
        //  survive, or the target re-applies look the server already owns.
        ApplySnapshot(context, 0);
        Tick(context, 300);

        Assert.True(context.Store.TryGet(context.Player, out TransformComponent transform));
        Assert.True(Quaternion.Dot(transform.Orientation, serverOrientation) > 0.9999f);
    }

    [Fact]
    public void IdleAngularVelocityAdvancesTheOrientationTarget()
    {
        TestContext context = CreateContext();
        var serverAngularVelocity = new Vector3(0f, 20f, 0f);

        ApplyTransform(context, context.SpawnPosition, Quaternion.Identity);
        ApplyPhysics(context, Vector3.Zero, serverAngularVelocity);
        ApplySnapshot(context, 0);
        Tick(context, 300);

        Assert.True(context.Store.TryGet(context.Player, out TransformComponent transform));
        Assert.True(transform.Orientation.W < 0.9f, "The target must advance with the idle angular velocity.");
    }

    [Fact]
    public void ServerAngularVelocityAppliesWhenLookIsIdle()
    {
        TestContext context = CreateContext();
        var serverAngularVelocity = new Vector3(0f, 1.5f, 0f);

        ApplyPhysics(context, Vector3.Zero, serverAngularVelocity);
        ApplySnapshot(context, 0);

        Assert.True(context.Store.TryGet(context.Player, out PhysicsComponent physics));
        Assert.Equal(serverAngularVelocity, physics.Torque);
    }

    [Fact]
    public void ServerAngularVelocityIsIgnoredWhileLookIsPending()
    {
        TestContext context = CreateContext();
        var localAngularVelocity = new Vector3(1f, 0f, 0f);
        var serverAngularVelocity = new Vector3(0f, 1.5f, 0f);

        SetInput(context, lookYaw: 0.5f);
        ApplyPhysics(context, Vector3.Zero, serverAngularVelocity);
        context.Store.AddOrUpdate(
            context.Player,
            new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Discrete, Vector3.Zero, localAngularVelocity)
        );
        ApplySnapshot(context, 0);

        Assert.True(context.Store.TryGet(context.Player, out PhysicsComponent physics));
        Assert.Equal(localAngularVelocity, physics.Torque);
    }

    [Fact]
    public void PlayerChangeResetsTheTargetState()
    {
        TestContext context = CreateContext();
        var firstOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1.5f);
        var secondOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -1f);

        //  Give the first player acked look the second player must not inherit.
        SetInput(context, lookYaw: 0.5f);
        AddPendingInput(context, lookYaw: 0.5f, serverTickAtSample: 0);
        ApplyTransform(context, context.ServerPosition, firstOrientation);
        ApplySnapshot(context, 0);

        Uuid secondUuid = Uuid.FromValue(43);
        int second = Seat(context.Store, secondUuid, context.SpawnPosition, context.SpawnOrientation);
        context.Reconciler.ApplyTransform(context.Store, second, context.ServerPosition, secondOrientation, Vector3.One);
        ApplySnapshot(context, 0);
        Tick(context, 300);

        Assert.True(context.Store.TryGet(second, out TransformComponent transform));
        Assert.True(Quaternion.Dot(transform.Orientation, secondOrientation) > 0.9999f);
    }

    private static TestContext CreateContext()
    {
        var store = new DataStore();
        var reconciler = new LocalPlayerReconciler(NullLogger<LocalPlayerReconciler>.Instance);
        Uuid playerUuid = Uuid.FromValue(42);
        var spawnPosition = new Vector3(1f, 2f, 3f);
        var spawnOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.25f);
        int player = Seat(store, playerUuid, spawnPosition, spawnOrientation);

        return new TestContext(store, reconciler, player, spawnPosition, spawnOrientation, new Vector3(4f, 5f, 6f));
    }

    private static int Seat(DataStore store, Uuid playerUuid, Vector3 position, Quaternion orientation)
    {
        int player = store.Alloc(playerUuid);
        store.AddOrUpdate(player, new PlayerComponent());
        store.AddOrUpdate(player, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Discrete));
        store.AddOrUpdate(player, new TransformComponent(position, orientation, Vector3.One));
        store.AddOrUpdate(player, new InputComponent());
        store.AddOrUpdate(player, PendingInputComponent.Create());
        return player;
    }

    private static void ApplyTransform(TestContext context, Vector3 position, Quaternion orientation)
    {
        context.Reconciler.ApplyTransform(context.Store, context.Player, position, orientation, Vector3.One);
    }

    private static void ApplyPhysics(TestContext context, Vector3 velocity, Vector3 angularVelocity)
    {
        context.Reconciler.ApplyPhysics(context.Store, context.Player, velocity, angularVelocity);
    }

    private static void ApplySnapshot(TestContext context, uint ackTick)
    {
        context.Reconciler.ApplySnapshot(context.Store, ackTick);
    }

    private static void Tick(TestContext context, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            context.Reconciler.Tick(1f / 128f, context.Store);
        }
    }

    private static void SetInput(TestContext context, float lookYaw)
    {
        context.Store.AddOrUpdate(context.Player, new InputComponent { LookYaw = lookYaw });
    }

    private static void AddPendingInput(TestContext context, float lookYaw, uint serverTickAtSample)
    {
        Assert.True(context.Store.TryGet(context.Player, out PendingInputComponent pending));
        pending.Ring!.Add(new InputComponent
        {
            LookYaw = lookYaw,
            ServerTickAtSample = serverTickAtSample,
        });
        context.Store.AddOrUpdate(context.Player, pending);
    }

    private static void TrimPendingInput(TestContext context, uint ackTick)
    {
        Assert.True(context.Store.TryGet(context.Player, out PendingInputComponent pending));
        pending.Ring!.TrimAtOrBelow(ackTick);
    }

    private sealed class TestContext(
        DataStore store,
        LocalPlayerReconciler reconciler,
        int player,
        Vector3 spawnPosition,
        Quaternion spawnOrientation,
        Vector3 serverPosition
    ) {
        public DataStore Store { get; } = store;
        public LocalPlayerReconciler Reconciler { get; } = reconciler;
        public int Player { get; } = player;
        public Vector3 SpawnPosition { get; } = spawnPosition;
        public Quaternion SpawnOrientation { get; } = spawnOrientation;
        public Vector3 ServerPosition { get; } = serverPosition;
    }
}
