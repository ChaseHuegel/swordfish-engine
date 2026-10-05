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
using WaywardBeyond.Client.Core.Events;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Client.Core.Networking;
using WaywardBeyond.Client.Core.Systems;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Server.Core;
using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Server.Core.Systems;
using WaywardBeyond.Shared.Bricks;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Transport;
using NUnit.Framework;

namespace WaywardBeyond.Client.Core.Tests;

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
        new NsdMessageSerializer<WorldEntityAdd>(),
        new NsdMessageSerializer<WorldStreamComplete>(),
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

        var replication = new NetworkReplicationSystem(hub, sessions, NullLogger<NetworkReplicationSystem>.Instance);
        var interaction = new ServerInteractionSystem(hub, new StubContent(), NullLogger<ServerInteractionSystem>.Instance, _ => new StubWorld(), new StubBrickIdMap());
        var join = new ServerJoinSystem(
            hub,
            sessions,
            new WorldSaveService(NullLogger<WorldSaveService>.Instance, () => throw new NotImplementedException(), new StubBrickIdMap()),
            replication,
            interaction,
            NullLogger<ServerJoinSystem>.Instance,
            new StubBrickIdMap()
        );

        connection.Client.Send(new JoinRequest
        {
            CharacterId = 100,
            PublicView = new PublicView { CharacterId = 100, Name = "P", Body = "wb:m_human" },
        });
        join.Tick(0f, store);
        replication.ApplyStage(0f, store);
        replication.SimTick = 2;
        replication.PublishStage(0f, store);

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

    private sealed class StubBrickIdMap : IBrickIdMap
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
}