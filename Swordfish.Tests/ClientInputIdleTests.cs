#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Physics;
using Xunit;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Networking.Components;

namespace Swordfish.Tests;

/// <summary>
/// Pins the idle-input policy: identical samples are not written or uploaded every tick, while a
/// change and the keepalive still produce a sample.
/// </summary>
public class ClientInputIdleTests
{
    [Fact]
    public void IdenticalIdleSamplesAreNotRewritten()
    {
        TestContext context = CreateContext();

        context.System.Tick(1f / 64f, context.Store);
        Assert.True(context.Store.IsDirty<InputComponent>(context.Entity), "The first sample must always be written.");
        Assert.Equal(1, PendingCount(context));

        context.Store.ClearDirty<InputComponent>(context.Entity);
        context.System.Tick(1f / 64f, context.Store);

        Assert.False(context.Store.IsDirty<InputComponent>(context.Entity), "An identical idle sample must not be written.");
        Assert.Equal(1, PendingCount(context));
    }

    [Fact]
    public void ChangedInputIsWrittenOnceUntilItChangesAgain()
    {
        TestContext context = CreateContext();

        context.System.Tick(1f / 64f, context.Store);
        context.Store.ClearDirty<InputComponent>(context.Entity);

        context.InputService.HeldKeys.Add(Key.W);
        context.System.Tick(1f / 64f, context.Store);
        Assert.True(context.Store.IsDirty<InputComponent>(context.Entity), "A changed sample must be written.");
        Assert.Equal(2, PendingCount(context));

        context.Store.ClearDirty<InputComponent>(context.Entity);
        context.System.Tick(1f / 64f, context.Store);
        Assert.False(context.Store.IsDirty<InputComponent>(context.Entity), "Holding a key must not resend the same sample.");
        Assert.Equal(2, PendingCount(context));
    }

    [Fact]
    public void KeepaliveResendsAnUnchangedSample()
    {
        TestContext context = CreateContext();

        context.System.Tick(1f / 64f, context.Store);
        context.Store.ClearDirty<InputComponent>(context.Entity);

        context.System.Tick(1.1f, context.Store);

        Assert.True(context.Store.IsDirty<InputComponent>(context.Entity), "The keepalive must resend an unchanged sample.");
    }

    private static TestContext CreateContext()
    {
        var inputService = new FakeInputService();
        var windowContext = new FakeWindowContext();
        var physics = new FakePhysics();
        var motionProcessor = new ClientPlayerMotionProcessor(
            inputService,
            windowContext,
            physics,
            new EventInvoker<PlayerMovedEvent>([])
        );
        motionProcessor.SetInputEnabled(true);

        var store = new DataStore();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new PlayerComponent());
        store.AddOrUpdate(entity, new InputComponent());
        store.AddOrUpdate(entity, new PendingInputComponent());

        var system = new ClientInputSystem(motionProcessor, inputService, new ControlSettings(), new SnapshotAckTracker());
        return new TestContext(store, entity, system, inputService);
    }

    private static int PendingCount(TestContext context)
    {
        Assert.True(context.Store.TryGet(context.Entity, out PendingInputComponent pending));
        return pending.PendingCount;
    }

    private sealed class TestContext(
        DataStore store,
        int entity,
        ClientInputSystem system,
        FakeInputService inputService
    ) {
        public DataStore Store { get; } = store;
        public int Entity { get; } = entity;
        public ClientInputSystem System { get; } = system;
        public FakeInputService InputService { get; } = inputService;
    }

    private sealed class FakePhysics : IPhysics
    {
        public float FixedTimestamp => 1f / 64f;

        public event EventHandler<EventArgs>? FixedUpdate
        {
            add { }
            remove { }
        }

        public RaycastResult Raycast(in Ray ray) => default;

        public void SetGravity(Vector3 gravity) { }
    }

    private sealed class FakeWindowContext : IWindowContext
    {
        public DataBinding<double> UpdateDelta { get; } = new(0d);

        public DataBinding<double> RenderDelta { get; } = new(0d);

        public Vector2 Resolution => Vector2.Zero;

        public Vector2 MonitorResolution => Vector2.Zero;

        public Action? Loaded { get; set; }

        public Action? Closed { get; set; }

        public Action<double>? Render { get; set; }

        public Action<double>? Update { get; set; }

        public Action? Focused { get; set; }

        public Action? Unfocused { get; set; }

        public Action<Vector2>? Resized { get; set; }

        public Vector2 GetSize() => Vector2.Zero;

        public void Close() { }

        public void SetIcon(Texture icon) { }
    }

    private sealed class FakeInputService : IInputService
    {
        public HashSet<Key> HeldKeys { get; } = [];

        public HashSet<MouseButton> HeldButtons { get; } = [];

        public InputDevice[] Devices => [];

        public InputDevice[] Mice => [];

        public InputDevice[] Keyboards => [];

        public InputDevice[] Gamepads => [];

        public InputDevice[] Joysticks => [];

        public InputDevice[] UnknownDevices => [];

        public EventHandler<ClickedEventArgs> Clicked { get; set; } = null!;

        public EventHandler<ClickedEventArgs> DoubleClicked { get; set; } = null!;

        public EventHandler<ScrolledEventArgs> Scrolled { get; set; } = null!;

        public EventHandler<CharEventArgs> CharInput { get; set; } = null!;

        public EventHandler<KeyEventArgs> KeyPressed { get; set; } = null!;

        public EventHandler<KeyEventArgs> KeyReleased { get; set; } = null!;

        public EventHandler<InputButtonEventArgs> ButtonPressed { get; set; } = null!;

        public EventHandler<InputButtonEventArgs> ButtonReleased { get; set; } = null!;

        public CursorOptions CursorOptions { get; set; }

        public Vector2 CursorDelta => Vector2.Zero;

        public Vector2 CursorPosition { get; set; }

        public bool IsMouseHeld(MouseButton mouseButton) => HeldButtons.Contains(mouseButton);

        public bool IsMousePressed(MouseButton mouseButton) => false;

        public bool IsMouseReleased(MouseButton mouseButton) => false;

        public float GetMouseScroll() => 0f;

        public bool IsKeyHeld(Key key) => HeldKeys.Contains(key);

        public bool IsKeyPressed(Key key) => false;

        public bool IsKeyReleased(Key key) => false;

        public bool IsButtonHeld(InputButton button) => false;

        public bool IsButtonPressed(InputButton button) => false;

        public bool IsButtonReleased(InputButton button) => false;

        public float GetAxis(InputAxis axis) => 0f;

        public float GetAxisDeadzone(InputAxis axis) => 0f;

        public void SetAxisDeadzone(InputAxis axis, float deadzone) { }

        public string GetClipboard() => string.Empty;

        public void SetClipboard(string content) { }
    }
}
