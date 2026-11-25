#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Krill;
using Swordfish.ECS;
using Swordfish.Library.Types.Shapes;
using Swordfish.Library.Util;
using Swordfish.Physics;
using Swordfish.Tests;
using Xunit;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Networking.Codecs;
using WaywardBeyond.Networking.Registry;

namespace WaywardBeyond.Tests;

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

    [Fact]
    public void RemotePlayerTransformCorrectionBlendsPosition()
    {
        TestContext context = CreateContext();
        Uuid remoteUuid = Uuid.FromValue(77);
        var initial = new Vector3(1f, 2f, 3f);
        var serverPosition = initial + new Vector3(1f, 0f, 0f);
        var serverOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1.5f);
        var serverScale = new Vector3(2f, 2f, 2f);
        int remote = context.Store.Alloc(remoteUuid);
        context.Store.AddOrUpdate(remote, new TransformComponent(initial, Quaternion.Identity, Vector3.One));
        context.Store.AddOrUpdate(remote, new BodyViewComponent { Body = "wb:human_m" });

        EnqueueTransform(context, remoteUuid, serverPosition, serverOrientation, serverScale);
        TickWhilePlaying(context, 1);

        Assert.True(context.Store.TryGet(remote, out TransformComponent transform));
        Assert.Equal(initial + new Vector3(0.5f, 0f, 0f), transform.Position);
        Assert.Equal(serverOrientation, transform.Orientation);
        Assert.Equal(serverScale, transform.Scale);
    }

    [Fact]
    public void RemotePlayerTransformCorrectionIgnoresDeadbandError()
    {
        TestContext context = CreateContext();
        Uuid remoteUuid = Uuid.FromValue(77);
        var initial = new Vector3(1f, 2f, 3f);
        var withinDeadband = initial + new Vector3(0.005f, 0f, 0f);
        int remote = context.Store.Alloc(remoteUuid);
        context.Store.AddOrUpdate(remote, new TransformComponent(initial, Quaternion.Identity, Vector3.One));
        context.Store.AddOrUpdate(remote, new BodyViewComponent { Body = "wb:human_m" });

        EnqueueTransform(context, remoteUuid, withinDeadband, Quaternion.Identity, Vector3.One);
        TickWhilePlaying(context, 1);

        Assert.True(context.Store.TryGet(remote, out TransformComponent transform));
        Assert.Equal(initial, transform.Position);
    }

    [Fact]
    public void WorldTransformSnapshotSnapsExactly()
    {
        TestContext context = CreateContext();
        Uuid structureUuid = Uuid.FromValue(88);
        var initial = new Vector3(1f, 2f, 3f);
        var serverPosition = initial + new Vector3(1f, 0f, 0f);
        int structure = context.Store.Alloc(structureUuid);
        context.Store.AddOrUpdate(structure, new TransformComponent(initial, Quaternion.Identity, Vector3.One));

        EnqueueTransform(context, structureUuid, serverPosition, Quaternion.Identity, Vector3.One);
        TickWhilePlaying(context, 1);

        Assert.True(context.Store.TryGet(structure, out TransformComponent transform));
        Assert.Equal(serverPosition, transform.Position);
    }

    [Fact]
    public void RemotePlayerPhysicsSnapshotCreatesComponentWithoutWarning()
    {
        var logger = new RecordingLogger();
        TestContext context = CreateContext(logger);
        Uuid remoteUuid = Uuid.FromValue(77);
        var position = new Vector3(7f, 8f, 9f);
        var velocity = new Vector3(1f, 2f, 3f);

        var remotePhysics = new PhysicsComponent(
            Layers.MOVING,
            BodyType.Dynamic,
            CollisionDetection.Continuous,
            velocity,
            Vector3.Zero
        );
        ColliderComponent remoteCollider = CharacterDefaults.CreateCharacterColliderComponent(Vector3.One);
        byte[] transformPayload = new TransformCodec().Serialize(new TransformComponent(position, Quaternion.Identity, Vector3.One));
        byte[] physicsPayload = new PhysicsCodec().Serialize(in remotePhysics);
        byte[] colliderPayload = new ColliderCodec().Serialize(in remoteCollider);

        var snapshot = new WorldSnapshot
        {
            TickNumber = 1,
            LastProcessedInput = 0,
            Components =
            [
                new ComponentSnapshot(remoteUuid.ToValue(), Uuid.FromValue(2).ToValue(), transformPayload),
                new ComponentSnapshot(remoteUuid.ToValue(), Uuid.FromValue(3).ToValue(), physicsPayload),
                new ComponentSnapshot(remoteUuid.ToValue(), Uuid.FromValue(17).ToValue(), colliderPayload),
            ],
            RemovedEntities = [],
        };
        context.Inbound.Snapshots.Enqueue(new ClientInbound.InboundSnapshot(snapshot));

        TickWhilePlaying(context, 1);

        Assert.True(context.Store.TryGet(remoteUuid, out int remote));
        Assert.True(context.Store.TryGet(remote, out TransformComponent transform));
        Assert.Equal(position, transform.Position);
        Assert.True(context.Store.TryGet(remote, out PhysicsComponent physics));
        Assert.Equal(Layers.MOVING, physics.Layer);
        Assert.Equal(BodyType.Dynamic, physics.BodyType);
        Assert.Equal(CollisionDetection.Continuous, physics.CollisionDetection);
        Assert.Equal(velocity, physics.Velocity);
        Assert.True(context.Store.TryGet(remote, out ColliderComponent collider));
        Assert.True(collider.CompoundShape.HasValue);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public void SameTickDespawnAndReallocationPreservesOneShotComponents()
    {
        TestContext context = CreateContext();
        Uuid remoteUuid = Uuid.FromValue(77);

        int existing = context.Store.Alloc(remoteUuid);
        context.Store.AddOrUpdate(existing, new TransformComponent(new Vector3(1f, 2f, 3f), Quaternion.Identity, Vector3.One));
        context.Store.AddOrUpdate(existing, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous));
        context.Store.AddOrUpdate(existing, CharacterDefaults.CreateCharacterColliderComponent(Vector3.One));
        context.Store.AddOrUpdate(existing, new BodyViewComponent { Body = "wb:human_m" });

        var newPosition = new Vector3(10f, 20f, 30f);
        var newVelocity = new Vector3(0.5f, 0f, -0.5f);
        var newPhysics = new PhysicsComponent(
            Layers.MOVING,
            BodyType.Dynamic,
            CollisionDetection.Continuous,
            newVelocity,
            Vector3.Zero
        );
        ColliderComponent newCollider = CharacterDefaults.CreateCharacterColliderComponent(Vector3.One);

        byte[] transformPayload = new TransformCodec().Serialize(new TransformComponent(newPosition, Quaternion.Identity, Vector3.One));
        byte[] physicsPayload = new PhysicsCodec().Serialize(in newPhysics);
        byte[] bodyViewPayload = SerializeBodyView("wb:human_f");
        byte[] colliderPayload = new ColliderCodec().Serialize(in newCollider);

        var snapshot = new WorldSnapshot
        {
            TickNumber = 1,
            LastProcessedInput = 0,
            Components =
            [
                new ComponentSnapshot(remoteUuid.ToValue(), Uuid.FromValue(2).ToValue(), transformPayload),
                new ComponentSnapshot(remoteUuid.ToValue(), Uuid.FromValue(3).ToValue(), physicsPayload),
                new ComponentSnapshot(remoteUuid.ToValue(), Uuid.FromValue(10).ToValue(), bodyViewPayload),
                new ComponentSnapshot(remoteUuid.ToValue(), Uuid.FromValue(17).ToValue(), colliderPayload),
            ],
            RemovedEntities = [remoteUuid.ToValue()],
        };
        context.Inbound.Snapshots.Enqueue(new ClientInbound.InboundSnapshot(snapshot));

        TickWhilePlaying(context, 1);

        Assert.True(context.Store.TryGet(remoteUuid, out int remote));
        Assert.True(context.Store.TryGet(remote, out TransformComponent transform));
        Assert.Equal(newPosition, transform.Position);
        Assert.True(context.Store.TryGet(remote, out PhysicsComponent physics));
        Assert.Equal(newVelocity, physics.Velocity);
        Assert.True(context.Store.TryGet(remote, out BodyViewComponent bodyView));
        Assert.Equal("wb:human_f", bodyView.Body);
        Assert.True(context.Store.TryGet(remote, out ColliderComponent collider));
        Assert.True(collider.CompoundShape.HasValue);
    }

    [Fact]
    public void NormalDespawnFreesRemoteEntity()
    {
        TestContext context = CreateContext();
        Uuid remoteUuid = Uuid.FromValue(77);

        int remote = context.Store.Alloc(remoteUuid);
        context.Store.AddOrUpdate(remote, new TransformComponent(new Vector3(1f, 2f, 3f), Quaternion.Identity, Vector3.One));
        context.Store.AddOrUpdate(remote, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous));
        context.Store.AddOrUpdate(remote, CharacterDefaults.CreateCharacterColliderComponent(Vector3.One));
        context.Store.AddOrUpdate(remote, new BodyViewComponent { Body = "wb:human_m" });

        var snapshot = new WorldSnapshot
        {
            TickNumber = 1,
            LastProcessedInput = 0,
            Components = [],
            RemovedEntities = [remoteUuid.ToValue()],
        };
        context.Inbound.Snapshots.Enqueue(new ClientInbound.InboundSnapshot(snapshot));

        TickWhilePlaying(context, 1);

        Assert.False(context.Store.TryGet(remoteUuid, out _));
    }

    [Fact]
    public void SameTickRejoinDespawnPreservesLocalPlayer()
    {
        TestContext context = CreateContext();
        byte[] transformPayload = new TransformCodec().Serialize(
            new TransformComponent(context.ServerPosition, Quaternion.Identity, Vector3.One));

        var snapshot = new WorldSnapshot
        {
            TickNumber = 1,
            LastProcessedInput = 0,
            Components =
            [
                new ComponentSnapshot(context.PlayerUuid.ToValue(), Uuid.FromValue(2).ToValue(), transformPayload),
            ],
            RemovedEntities = [context.PlayerUuid.ToValue()],
        };
        context.Inbound.Snapshots.Enqueue(new ClientInbound.InboundSnapshot(snapshot));

        TickWhilePlaying(context, 1);

        Assert.True(context.Store.TryGet(context.PlayerUuid, out int player));
        Assert.True(context.Store.TryGet(player, out PlayerComponent _));
        Assert.True(context.Store.TryGet(player, out PendingInputComponent _));
    }

    private static byte[] SerializeBodyView(string body)
    {
        var store = new DataStore();
        int entity = store.Alloc(Uuid.FromValue(1));
        store.AddOrUpdate(entity, new BodyViewComponent { Body = body });
        Result<byte[]> serialized = new NsdComponentCodec<BodyViewComponent>().Serialize(store, entity);
        Assert.True(serialized.Success);
        return serialized.Value;
    }

    private static TestContext CreateContext(ILogger<ClientReconcileSystem>? logger = null)
    {
        var inbound = new ClientInbound();
        var registry = new NetworkRegistry();
        Assert.True(registry.Register<TransformComponent>(Uuid.FromValue(2), NetworkDirection.ServerOwned, new TransformCodec()).Success);
        Assert.True(registry.Register<PhysicsComponent>(Uuid.FromValue(3), NetworkDirection.ServerOwned, new PhysicsCodec()).Success);
        Assert.True(registry.Register<BodyViewComponent>(Uuid.FromValue(10), NetworkDirection.ServerOwned, new NsdComponentCodec<BodyViewComponent>()).Success);
        Assert.True(registry.Register<ColliderComponent>(Uuid.FromValue(17), NetworkDirection.ServerOwned, new ColliderCodec()).Success);
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
            logger ?? NullLogger<ClientReconcileSystem>.Instance
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

    private static void EnqueueTransform(
        TestContext context,
        Uuid entityUuid,
        Vector3 position,
        Quaternion orientation,
        Vector3 scale
    ) {
        byte[] payload = new TransformCodec().Serialize(new TransformComponent(position, orientation, scale));
        var snapshot = new WorldSnapshot
        {
            TickNumber = 1,
            LastProcessedInput = 0,
            Components = [new ComponentSnapshot(entityUuid.ToValue(), Uuid.FromValue(2).ToValue(), payload)],
            RemovedEntities = [],
        };

        context.Inbound.Snapshots.Enqueue(new ClientInbound.InboundSnapshot(snapshot));
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

    private sealed class RecordingLogger : ILogger<ClientReconcileSystem>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) {
            Entries.Add((logLevel, formatter(state, exception)));
        }
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
