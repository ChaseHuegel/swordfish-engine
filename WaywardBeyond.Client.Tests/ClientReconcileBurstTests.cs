using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.Physics;
using WaywardBeyond.Client;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Components;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Networking.Transport;
using NUnit.Framework;

namespace WaywardBeyond.Client.Tests;

/// <summary>
/// A snapshot burst queued during Loading must be coalesced on entry to Playing: the first play tick
/// applies exactly one snapshot (the newest), so a backlog left over from a previous join can never
/// hitch the first play frame.
/// </summary>
public class ClientReconcileBurstTests
{
    private sealed class QueuedConnection : IClientConnection
    {
        private readonly Queue<WorldSnapshot> _messages = new();

        public bool IsConnected => true;
        public bool IsLocal => false;

        public void Queue(WorldSnapshot snapshot)
        {
            _messages.Enqueue(snapshot);
        }

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

    [Test]
    public void ComponentRemovalClearsClientSideAndEntitySurvives()
    {
        NetworkRegistry.Initialize([typeof(InputComponent).Assembly]);

        var connection = new QueuedConnection();
        var tracker = new SnapshotAckTracker();
        var system = new ClientReconcileSystem(connection, tracker, new ClientPlayerMotionProcessor(
            new StubInputService(),
            new StubWindow(),
            new StubPhysics(),
            new EventInvoker<PlayerMovedEvent>([])
        ));
        var store = new Swordfish.ECS.DataStore();

        //  A remote entity with a replicated component.
        var uuid = Swordfish.ECS.Uuid.FromValue(0xCAFE);
        int entity = store.Alloc(uuid);
        store.AddOrUpdate(entity, new InputComponent());
        store.AddOrUpdate(entity, new NetworkComponent());

        //  The server removes the component and echoes the removal.
        var removalTypeUuid = new NetworkComponentAttribute(1).Uuid;
        connection.Queue(new WorldSnapshot
        {
            TickNumber = 5,
            LastProcessedInput = 0,
            Components = [],
            RemovedEntities = [],
            RemovedComponents = [new ComponentRemoval { Entity = uuid.ToValue(), TypeUuid = removalTypeUuid.ToValue() }],
        });

        GameState prior = WaywardBeyond.GameState.Get();
        WaywardBeyond.GameState.Set(GameState.Playing);
        try
        {
            system.Tick(0f, store);

            Assert.That(store.Has(typeof(InputComponent), entity), Is.False, "The removed component clears client-side.");
            Assert.That(store.Has(typeof(NetworkComponent), entity), Is.True, "Unrelated components stay.");
            Assert.That(store.TryGet(uuid, out _), Is.True, "The entity survives removal as a bare entity.");
            Assert.That(tracker.LastAppliedSnapshotTick, Is.EqualTo(5u));
        }
        finally
        {
            WaywardBeyond.GameState.Set(prior);
        }
    }

    private sealed class StubWindow : IWindowContext
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
        public float GetMouseScroll() => 0f;
        public bool IsKeyHeld(Key key) => false;
        public bool IsKeyPressed(Key key) => false;
        public bool IsKeyReleased(Key key) => false;
        public bool IsButtonHeld(InputButton button) => false;
        public bool IsButtonPressed(InputButton button) => false;
        public bool IsButtonReleased(InputButton button) => false;
        public float GetAxis(InputAxis axis) => 0f;
        public float GetAxisDeadzone(InputAxis axis) => 0f;
        public void SetAxisDeadzone(InputAxis axis, float value) { }
        public string GetClipboard() => string.Empty;
        public void SetClipboard(string text) { }
    }

    private sealed class StubPhysics : IPhysics
    {
        public event EventHandler<EventArgs>? FixedUpdate;
        public RaycastResult Raycast(in Ray ray) => default;
        public void SetGravity(Vector3 gravity) { }
    }

    [Test]
    public void EnteringPlayAppliesOnlyTheNewestQueuedSnapshot()
    {
        var connection = new QueuedConnection();
        var tracker = new SnapshotAckTracker();
        var motion = new ClientPlayerMotionProcessor(
            new StubInputService(),
            new StubWindow(),
            new StubPhysics(),
            new EventInvoker<PlayerMovedEvent>([])
        );
        var system = new ClientReconcileSystem(connection, tracker, motion);
        var store = new Swordfish.ECS.DataStore();

        //  Three snapshots pile up while the client is Loading.
        connection.Queue(new WorldSnapshot { TickNumber = 1, Components = [], RemovedEntities = [] });
        connection.Queue(new WorldSnapshot { TickNumber = 2, Components = [], RemovedEntities = [] });
        connection.Queue(new WorldSnapshot { TickNumber = 3, Components = [], RemovedEntities = [] });

        GameState prior = WaywardBeyond.GameState.Get();
        WaywardBeyond.GameState.Set(GameState.Loading);
        try
        {
            system.Tick(0f, store);
            Assert.That(tracker.LastAppliedSnapshotTick, Is.EqualTo(0u), "Nothing applies while Loading.");

            WaywardBeyond.GameState.Set(GameState.Playing);
            system.Tick(0f, store);

            //  Exactly one apply happened, and it was the newest queued frame.
            Assert.That(tracker.LastAppliedSnapshotTick, Is.EqualTo(3u));
        }
        finally
        {
            WaywardBeyond.GameState.Set(prior);
        }
    }
}