#nullable enable
using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Util;
using Swordfish.Physics;
using Xunit;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking.Codecs;
using WaywardBeyond.Networking.Registry;

namespace Swordfish.Tests;

/// <summary>Pins the reconcile system's local-player dispatch, trim, and orientation wiring.</summary>
[Collection("ClientGameState")]
public class ClientReconcileSystemTests
{
    [Fact]
    public void LocalPlayerSnapshotReachesReconciler()
    {
        TestContext context = CreateContext();
        var serverOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1.5f);
        var pendingLook = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f);
        Quaternion target = Quaternion.Normalize(serverOrientation * pendingLook);
        var serverVelocity = new Vector3(1f, 2f, 3f);

        SetInput(context, lookYaw: 0.5f);
        AddPendingInput(context, lookYaw: 0.5f, serverTickAtSample: 1);
        EnqueueTransformAndPhysics(
            context,
            context.ServerPosition,
            serverOrientation,
            Vector3.One,
            serverVelocity,
            Vector3.Zero
        );

        TickWhilePlaying(context, 300);

        Assert.True(context.Store.TryGet(context.Player, out TransformComponent transform));
        Assert.Equal(context.ServerPosition, transform.Position);
        Assert.Equal(Vector3.One, transform.Scale);
        Assert.True(Quaternion.Dot(transform.Orientation, target) > 0.9999f);

        Assert.True(context.Store.TryGet(context.Player, out PhysicsComponent physics));
        Assert.Equal(serverVelocity, physics.Velocity);
    }

    [Fact]
    public void SnapshotTrimsAckedInputAndAppliesIdleAngularVelocity()
    {
        TestContext context = CreateContext();
        var serverAngularVelocity = new Vector3(0f, 1.5f, 0f);

        AddPendingInput(context, lookYaw: 0f, serverTickAtSample: 0);
        EnqueueTransformAndPhysics(
            context,
            context.ServerPosition,
            Quaternion.Identity,
            Vector3.One,
            Vector3.Zero,
            serverAngularVelocity
        );

        TickWhilePlaying(context, 1);

        Assert.True(context.Store.TryGet(context.Player, out PendingInputComponent pending));
        Assert.Equal(0, pending.Ring!.Count);

        Assert.True(context.Store.TryGet(context.Player, out PhysicsComponent physics));
        Assert.Equal(serverAngularVelocity, physics.Torque);
    }

    private static TestContext CreateContext()
    {
        var inbound = new ClientInbound();
        var registry = new NetworkRegistry();
        Assert.True(registry.Register<TransformComponent>(Uuid.FromValue(2), NetworkDirection.ServerOwned, new TransformCodec()).Success);
        Assert.True(registry.Register<PhysicsComponent>(Uuid.FromValue(3), NetworkDirection.ServerOwned, new PhysicsCodec()).Success);
        registry.Freeze();

        var motionProcessor = new ClientPlayerMotionProcessor(
            new FakeInputService(),
            new FakeWindowContext(),
            new FakePhysics(),
            new EventInvoker<PlayerMovedEvent>([])
        );

        var system = new ClientReconcileSystem(
            inbound,
            new SnapshotAckTracker(),
            motionProcessor,
            new LocalPlayerReconciler(NullLogger<LocalPlayerReconciler>.Instance),
            registry,
            NullLogger<ClientReconcileSystem>.Instance
        );

        var store = new DataStore();
        Uuid playerUuid = Uuid.FromValue(42);
        var spawnPosition = new Vector3(1f, 2f, 3f);
        var spawnOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.25f);
        int player = Seat(store, playerUuid, spawnPosition, spawnOrientation);

        return new TestContext(store, inbound, system, player, playerUuid, new Vector3(4f, 5f, 6f));
    }

    private static void TickWhilePlaying(TestContext context, int ticks)
    {
        WaywardBeyond.Client.WaywardBeyond.GameState.Set(WaywardBeyond.Client.GameState.Playing);
        try
        {
            for (var i = 0; i < ticks; i++)
            {
                context.System.Tick(1f / 128f, context.Store);
            }
        }
        finally
        {
            WaywardBeyond.Client.WaywardBeyond.GameState.Set(WaywardBeyond.Client.GameState.MainMenu);
        }
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

    private static void EnqueueTransformAndPhysics(
        TestContext context,
        Vector3 position,
        Quaternion orientation,
        Vector3 scale,
        Vector3 velocity,
        Vector3 angularVelocity,
        uint ackTick = 0
    ) {
        byte[] transformPayload = new TransformCodec().Serialize(new TransformComponent(position, orientation, scale));
        var physics = new PhysicsComponent(
            Layers.MOVING,
            BodyType.Dynamic,
            CollisionDetection.Discrete,
            velocity,
            angularVelocity
        );
        byte[] physicsPayload = new PhysicsCodec().Serialize(in physics);

        var snapshot = new WorldSnapshot
        {
            TickNumber = 1,
            LastProcessedInput = ackTick,
            Components =
            [
                new ComponentSnapshot(context.PlayerUuid.ToValue(), Uuid.FromValue(2).ToValue(), transformPayload),
                new ComponentSnapshot(context.PlayerUuid.ToValue(), Uuid.FromValue(3).ToValue(), physicsPayload),
            ],
            RemovedEntities = [],
        };

        context.Inbound.Snapshots.Enqueue(new ClientInbound.InboundSnapshot(snapshot));
    }

    private sealed class TestContext(
        DataStore store,
        ClientInbound inbound,
        ClientReconcileSystem system,
        int player,
        Uuid playerUuid,
        Vector3 serverPosition
    ) {
        public DataStore Store { get; } = store;
        public ClientInbound Inbound { get; } = inbound;
        public ClientReconcileSystem System { get; } = system;
        public int Player { get; } = player;
        public Uuid PlayerUuid { get; } = playerUuid;
        public Vector3 ServerPosition { get; } = serverPosition;
    }
}

/// <summary>Serializes tests that mutate the process-wide client game state.</summary>
[CollectionDefinition("ClientGameState", DisableParallelization = true)]
public class ClientGameStateCollection;
