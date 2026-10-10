using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.Physics;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Voxels;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Networking;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Networking.Components;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Networking.Serialization;
using WaywardBeyond.Networking.Transport;
using NUnit.Framework;

using WaywardBeyond.Config;

namespace WaywardBeyond.Client.Tests;

/// <summary>
/// The seeded interaction context (including the server-granted starter inventory) must reach the
/// client's local player through the real reconcile system.
/// </summary>
public class ClientInventoryEchoTests
{
    private static readonly INetworkSerializer[] Serializers =
    [
        new NsdMessageSerializer<JoinRequest>(),
        new NsdMessageSerializer<JoinAccept>(),
        new NsdMessageSerializer<LevelEntityAdd>(),
        new NsdMessageSerializer<LevelStreamComplete>(),
        new NsdMessageSerializer<WorldSnapshot>(),
    ];

    [Test]
    public void ServerEchoFillsTheLocalPlayerInventory()
    {
        NetworkRegistry.Initialize([typeof(InputComponent).Assembly]);

        //  A live server: hub + join + replication, exactly the wire flow a world would run.
        var connection = new LocalConnection(Serializers);
        var hub = new ServerConnectionHub();
        Uuid clientId = hub.Add(connection.Server);
        var sessions = new SessionManager();
        var store = new DataStore();

        var replication = new NetworkReplicationSystem(hub, sessions, NullLogger<NetworkReplicationSystem>.Instance, new NetworkingConfig());
        var interaction = new ServerInteractionSystem(hub, new StubContent(), NullLogger<ServerInteractionSystem>.Instance, _ => new StubWorld(), new StubBrickRegistry());
        var join = new ServerJoinSystem(
            hub,
            sessions,
            new LevelSaveService(NullLogger<LevelSaveService>.Instance, new StubLevelCatalog(), new StubBrickRegistry()),
            replication,
            interaction,
            NullLogger<ServerJoinSystem>.Instance,
            new StubBrickRegistry()
        );

        connection.Client.Send(new JoinRequest
        {
            CharacterId = 100,
            PublicView = new PublicView { CharacterId = 100, Name = "P", Body = "wb:m_human" },
        });
        join.Tick(0f, store);
        replication.ApplyStage(0f, store);
        replication.SimTick = 2;
        replication.PublishStage(1f, store);

        Result<JoinAccept> accept = connection.Client.Receive<JoinAccept>();
        Assert.That(accept.Success, Is.True, "Join must complete.");

        //  The client seats its player (mirroring PlayerCharacterEntityBuilder's fresh-character build)
        //  and drives the real reconcile system through its entering-play burst path.
        var clientStore = new DataStore();
        int player = clientStore.Alloc(Uuid.FromValue(accept.Value.PlayerEntity));
        clientStore.AddOrUpdate(player, new InventoryComponent());

        var tracker = new SnapshotAckTracker();
        var reconcile = new ClientReconcileSystem(connection.Client, tracker, new ClientPlayerMotionProcessor(
            new StubInputService(),
            new StubWindowContext(),
            new StubPhysics(),
            new EventInvoker<PlayerMovedEvent>([])
        ));

        GameState prior = WaywardBeyond.GameState.Get();
        WaywardBeyond.GameState.Set(GameState.Playing);
        try
        {
            reconcile.Tick(0f, clientStore);
            reconcile.Tick(0f, clientStore);
        }
        finally
        {
            WaywardBeyond.GameState.Set(prior);
        }

        Assert.That(clientStore.TryGet(player, out InventoryComponent inventory), Is.True);
        Assert.That(System.Linq.Enumerable.Any(inventory.Contents, item => item.ID == "laser" && item.Count > 0),
            Is.True, "The server-granted starter inventory must land on the local player.");
    }

    private sealed class StubBrickRegistry : IBrickRegistry
    {
        public int Count => 0;
        public ushort Id(string name) => 0;
        public string? Name(ushort id) => null;
    }

    private sealed class StubContent : IInteractionContent
    {
        public bool TryGetPlaceable(string? itemID, out PlaceableBrick placeable)
        {
            placeable = default;
            return false;
        }

        public bool TryGetLoot(ushort brickDataID, out ItemData loot)
        {
            loot = default;
            return false;
        }
    }

    private sealed class StubWorld : IVoxelInteractionWorld
    {
        public bool TryGetVoxelTarget(int entity, out VoxelObject? voxelObject, out TransformComponent transform)
        {
            voxelObject = null;
            transform = default;
            return false;
        }

        public bool TryGetVoxelTarget(in Uuid entityUuid, out int entity, out VoxelObject? voxelObject, out TransformComponent transform)
        {
            entity = default;
            voxelObject = null;
            transform = default;
            return false;
        }
    }

    private sealed class StubPhysics : IPhysics
    {
        public event EventHandler<EventArgs>? FixedUpdate;
        public RaycastResult Raycast(in Ray ray) => default;
        public void SetGravity(Vector3 gravity) { }
    }

    private sealed class StubWindowContext : IWindowContext
    {
        public DataBinding<double> UpdateDelta { get; } = new();
        public DataBinding<double> RenderDelta { get; } = new();
        public Vector2 Resolution => default;
        public Vector2 MonitorResolution => default;
        public Action? Loaded { get; set; }
        public Action? Closed { get; set; }
        public Action<double>? Render { get; set; }
        public Action<double>? Update { get; set; }
        public Action? Focused { get; set; }
        public Action? Unfocused { get; set; }
        public Action<Vector2>? Resized { get; set; }
        public Vector2 GetSize() => default;
        public void Close() { }
        public void SetIcon(Texture icon) { }
    }

    private sealed class StubInputService : IInputService
    {
        public InputDevice[] Devices => [];
        public InputDevice[] Mice => [];
        public InputDevice[] Keyboards => [];
        public InputDevice[] Gamepads => [];
        public InputDevice[] Joysticks => [];
        public InputDevice[] UnknownDevices => [];
        public EventHandler<ClickedEventArgs> Clicked { get; set; }
        public EventHandler<ClickedEventArgs> DoubleClicked { get; set; }
        public EventHandler<ScrolledEventArgs> Scrolled { get; set; }
        public EventHandler<CharEventArgs> CharInput { get; set; }
        public EventHandler<KeyEventArgs> KeyPressed { get; set; }
        public EventHandler<KeyEventArgs> KeyReleased { get; set; }
        public EventHandler<InputButtonEventArgs> ButtonPressed { get; set; }
        public EventHandler<InputButtonEventArgs> ButtonReleased { get; set; }
        public CursorOptions CursorOptions { get; set; }
        public Vector2 CursorDelta => default;
        public Vector2 CursorPosition { get; set; }
        public bool IsMouseHeld(MouseButton mouseButton) => false;
        public bool IsMousePressed(MouseButton mouseButton) => false;
        public bool IsMouseReleased(MouseButton mouseButton) => false;
        public bool IsKeyHeld(Key key) => false;
        public bool IsKeyPressed(Key key) => false;
        public float GetMouseScroll() => 0f;
        public bool IsKeyReleased(Key key) => false;
        public bool IsButtonHeld(InputButton button) => false;
        public bool IsButtonPressed(InputButton button) => false;
        public bool IsButtonReleased(InputButton button) => false;
        public float GetAxis(InputAxis axis) => 0f;
        public float GetAxisDeadzone(InputAxis axis) => 0f;
        public void SetAxisDeadzone(InputAxis axis, float deadzone) { }
        public string GetClipboard() => string.Empty;
        public void SetClipboard(string text) { }
    }

    [Test]
    public void EnteringPlayBurstKeepsOneShotStateSupersededByMotion()
    {
        NetworkRegistry.Initialize([typeof(InputComponent).Assembly]);
        NetworkRegistry.Register<TransformComponent>(Uuid.FromValue(2), NetworkDirection.ServerOwned, new TransformCodec());

        //  The server's join burst: frame 1 carries the seeded inventory (one-shot), and the frames that
        //  follow it - once the player moves - carry only the motion pair; the entering-play coalesce
        //  keeps only the newest frame, so the one-shot state must be applied from the superseded frames.
        var store = new DataStore();
        const ulong playerUuidValue = 0xB0B;
        int player = store.Alloc(Uuid.FromValue(playerUuidValue));
        var seededInventory = new InventoryComponent();
        seededInventory.Add(InventoryComponent.Stack("laser", 1, 1));
        store.AddOrUpdate(player, seededInventory);

        NetworkRegistry.TryGetInfo<InventoryComponent>(out NetworkComponentInfo inventoryInfo);
        byte[] inventoryPayload = inventoryInfo.Codec.Serialize(store, player);
        NetworkRegistry.TryGetInfo<TransformComponent>(out NetworkComponentInfo transformInfo);
        store.AddOrUpdate(player, new TransformComponent(new Vector3(0f, 1f, 0f), Quaternion.Identity));
        byte[] transformPayload = transformInfo.Codec.Serialize(store, player);

        var connection = new QueuedConnection();
        connection.Queue(new WorldSnapshot
        {
            TickNumber = 1,
            Components = [new ComponentSnapshot(playerUuidValue, inventoryInfo.Uuid.ToValue(), inventoryPayload)],
        });
        connection.Queue(new WorldSnapshot
        {
            TickNumber = 2,
            Components = [new ComponentSnapshot(playerUuidValue, transformInfo.Uuid.ToValue(), transformPayload)],
        });

        var tracker = new SnapshotAckTracker();
        var reconcile = new ClientReconcileSystem(connection, tracker, new ClientPlayerMotionProcessor(
            new StubInputService(),
            new StubWindowContext(),
            new StubPhysics(),
            new EventInvoker<PlayerMovedEvent>([])
        ));

        GameState prior = WaywardBeyond.GameState.Get();
        WaywardBeyond.GameState.Set(GameState.Playing);
        try
        {
            reconcile.Tick(0f, store);
        }
        finally
        {
            WaywardBeyond.GameState.Set(prior);
        }

        Assert.That(store.TryGet(player, out InventoryComponent inventory), Is.True);
        Assert.That(System.Linq.Enumerable.Any(inventory.Contents, item => item.ID == "laser" && item.Count > 0),
            Is.True, "The one-shot starter inventory must survive the motion-superseding burst.");
        Assert.That(store.TryGet(player, out TransformComponent transform), Is.True);
        Assert.That(transform.Position.Y, Is.EqualTo(1f), "The newest frame's motion wins.");
    }

    private sealed class QueuedConnection : IClientConnection
    {
        private readonly Queue<WorldSnapshot> _messages = new();
        public bool IsConnected => true;
        public bool IsLocal => false;
        public void Queue(WorldSnapshot snapshot) => _messages.Enqueue(snapshot);
        public Result Send<T>(in T message) => Result.FromSuccess();
        public Result<T> Receive<T>()
        {
            if (typeof(T) == typeof(WorldSnapshot) && _messages.Count > 0)
            {
                return Result<T>.FromSuccess((T)(object)_messages.Dequeue());
            }

            return Result<T>.FromFailure("No messages available.");
        }
    }
}
