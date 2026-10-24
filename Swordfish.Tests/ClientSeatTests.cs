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
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Networking.Codecs;
using WaywardBeyond.Networking.Registry;

namespace Swordfish.Tests;

/// <summary>
/// Pins the client seat contract: the join accept seeds the local spawn, and reconcile never applies
/// the authoritative orientation over the locally predicted look.
/// </summary>
[Collection("ClientGameState")]
public class ClientSeatTests
{
    [Fact]
    public void DecorateSeatsTheAuthoritativeSpawnAndResetsPredictionState()
    {
        var store = new DataStore();
        var builder = new PlayerCharacterEntityBuilder(new FakeRenderContext(store));
        int entity = store.Alloc();

        var character = new Character(
            default,
            1UL,
            _Playtime: new Playtime(),
            "Test",
            0, 0, 0, 0, 0, 0,
            _Body: string.Empty,
            _ActiveInventorySlot: 0,
            _GameMode: GameMode.Creative,
            _Statistics: null,
            _Inventory: null
        );

        var position = new Vector3(1f, 2f, 3f);
        var orientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f);

        builder.Decorate(new Entity(entity, store), character, position, orientation);

        Assert.True(store.TryGet(entity, out TransformComponent transform));
        Assert.Equal(position, transform.Position);
        Assert.Equal(orientation, transform.Orientation);
        Assert.Equal(CharacterDefaults.PlayerScale, transform.Scale);

        Assert.True(store.TryGet(entity, out PendingInputComponent pending));
        Assert.NotNull(pending.Ring);

        Assert.True(store.TryGet(entity, out InputComponent input));
        Assert.Equal(0f, input.LookPitch);
        Assert.Equal(0f, input.LookYaw);
        Assert.Equal(0f, input.LookRoll);
    }

    [Fact]
    public void LocalPlayerKeepsItsPredictedOrientationAcrossSessions()
    {
        var store = new DataStore();
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
            in inbound,
            new SnapshotAckTracker(),
            in motionProcessor,
            registry,
            NullLogger<ClientReconcileSystem>.Instance
        );

        Uuid playerUuid = Uuid.FromValue(42);
        var spawnPosition = new Vector3(1f, 2f, 3f);
        var spawnOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.25f);
        var serverPosition = new Vector3(4f, 5f, 6f);
        var serverOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1.5f);
        var scale = Vector3.One;

        int player = Seat(store, playerUuid, spawnPosition, spawnOrientation);
        EnqueueTransform(inbound, playerUuid, serverPosition, serverOrientation, scale);

        WaywardBeyond.Client.WaywardBeyond.GameState.Set(WaywardBeyond.Client.GameState.Playing);
        try
        {
            system.Tick(1f / 64f, store);

            Assert.True(store.TryGet(player, out TransformComponent first));
            Assert.Equal(serverPosition, first.Position);
            Assert.Equal(spawnOrientation, first.Orientation);
            Assert.Equal(scale, first.Scale);

            //  A same-character rejoin reuses the identity. The new seat seeds a new spawn, and the
            //  authoritative echo still never overwrites the locally predicted look.
            store.Free(player);

            var nextSpawnOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.75f);
            int rejoined = Seat(store, playerUuid, spawnPosition, nextSpawnOrientation);
            EnqueueTransform(inbound, playerUuid, serverPosition + Vector3.One, serverOrientation, scale);

            system.Tick(1f / 64f, store);

            Assert.True(store.TryGet(rejoined, out TransformComponent second));
            Assert.Equal(serverPosition + Vector3.One, second.Position);
            Assert.Equal(nextSpawnOrientation, second.Orientation);
            Assert.Equal(scale, second.Scale);
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
        return player;
    }

    private static void EnqueueTransform(
        ClientInbound inbound,
        Uuid playerUuid,
        Vector3 position,
        Quaternion orientation,
        Vector3 scale
    ) {
        byte[] payload = new TransformCodec().Serialize(new TransformComponent(position, orientation, scale));
        var snapshot = new WorldSnapshot
        {
            TickNumber = 1,
            LastProcessedInput = 0,
            Components =
            [
                new ComponentSnapshot(playerUuid.ToValue(), Uuid.FromValue(2).ToValue(), payload),
            ],
            RemovedEntities = [],
        };

        inbound.Snapshots.Enqueue(new ClientInbound.InboundSnapshot(snapshot));
    }
}

/// <summary>Serializes tests that mutate the process-wide client game state.</summary>
[CollectionDefinition("ClientGameState", DisableParallelization = true)]
public class ClientGameStateCollection;
