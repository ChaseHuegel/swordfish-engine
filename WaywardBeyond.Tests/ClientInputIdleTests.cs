#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Input;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Physics;
using Swordfish.Tests;
using Xunit;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Shortcuts;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Config;
using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Tests;

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

    [Fact]
    public void ResetSessionClearsLookTotalsAndWritesTheFirstSample()
    {
        TestContext context = CreateContext();

        context.MotionProcessor.Tick(1f / 64f, context.Store);
        context.MotionProcessor.Step!.AlignTo(5000);
        context.MotionProcessor.CursorUpdates.Enqueue(new Vector2(10f, 0f));
        context.System.Tick(1f / 64f, context.Store);
        Assert.True(context.Store.TryGet(context.Entity, out InputComponent before));
        Assert.NotEqual(0f, before.LookYaw);

        context.Store.ClearDirty<InputComponent>(context.Entity);
        context.System.ResetSession(context.Store.GetUuid(context.Entity));
        Assert.Equal(0u, context.MotionProcessor.Step.CurrentSimTick);
        context.System.Tick(1f / 64f, context.Store);

        Assert.True(context.Store.TryGet(context.Entity, out InputComponent after));
        Assert.Equal(0f, after.LookPitch);
        Assert.Equal(0f, after.LookYaw);
        Assert.Equal(0f, after.LookRoll);
        Assert.True(context.Store.IsDirty<InputComponent>(context.Entity), "The reset must force a first sample.");
        Assert.True(context.Store.TryGet(context.Entity, out PendingInputComponent pending));
        Assert.Equal(0u, pending.Ring![pending.Ring.Count - 1].StagedAtSimTick);
    }

    [Fact]
    public void MovementAndPrimaryActionFollowReboundShortcuts()
    {
        TestContext context = CreateContext();

        Assert.True(context.ShortcutService.Rebind(GameShortcuts.MOVE_FORWARD, new ShortcutBinding(Key.UpArrow)).Success);
        Assert.True(context.ShortcutService.Rebind(GameShortcuts.PRIMARY_ACTION, new ShortcutBinding(Key.F)).Success);

        context.InputService.HeldKeys.Add(Key.W);
        context.InputService.HeldButtons.Add(MouseButton.Left);
        context.System.Tick(1f / 64f, context.Store);
        Assert.True(context.Store.TryGet(context.Entity, out InputComponent oldKeys));
        Assert.Equal(0f, oldKeys.MovementZ);
        Assert.False(oldKeys.PrimaryHeld);

        context.InputService.HeldKeys.Add(Key.UpArrow);
        context.InputService.HeldKeys.Add(Key.F);
        context.System.Tick(1f / 64f, context.Store);
        Assert.True(context.Store.TryGet(context.Entity, out InputComponent reboundKeys));
        Assert.Equal(-1f, reboundKeys.MovementZ);
        Assert.True(reboundKeys.PrimaryHeld);
    }

    [Fact]
    public void PrimaryHeldCarriesSelectedColorPaintAndRedirtiesOnSwatchChange()
    {
        var inputService = new FakeInputService();
        var shortcutService = new ShortcutService(inputService, NullLogger.Instance);
        var windowContext = new FakeWindowContext();
        var physics = new FakePhysics();
        var motionProcessor = new ClientPlayerMotionProcessor(
            inputService,
            windowContext,
            physics,
            new EventInvoker<PlayerMovedEvent>([])
        );
        motionProcessor.SetInputEnabled(true);
        var interactionState = new InteractionState(in motionProcessor);
        interactionState.SelectColorPaint("wb:red");

        var store = new DataStore();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new PlayerComponent());
        store.AddOrUpdate(entity, new InputComponent());
        store.AddOrUpdate(entity, new PendingInputComponent());

        var system = new ClientInputSystem(motionProcessor, shortcutService, new ControlSettings(), new SnapshotAckTracker(), interactionState);

        system.Tick(1f / 64f, store);
        Assert.True(store.TryGet(entity, out InputComponent idle));
        Assert.False(idle.PrimaryHeld);
        Assert.Null(idle.PaintId);

        store.ClearDirty<InputComponent>(entity);
        inputService.HeldButtons.Add(MouseButton.Left);
        system.Tick(1f / 64f, store);
        Assert.True(store.IsDirty<InputComponent>(entity));
        Assert.True(store.TryGet(entity, out InputComponent firingRed));
        Assert.True(firingRed.PrimaryHeld);
        Assert.Equal("wb:red", firingRed.PaintId);

        store.ClearDirty<InputComponent>(entity);
        system.Tick(1f / 64f, store);
        Assert.False(store.IsDirty<InputComponent>(entity));

        interactionState.SelectColorPaint("wb:cyan");
        system.Tick(1f / 64f, store);
        Assert.True(store.IsDirty<InputComponent>(entity));
        Assert.True(store.TryGet(entity, out InputComponent firingCyan));
        Assert.Equal("wb:cyan", firingCyan.PaintId);

        store.ClearDirty<InputComponent>(entity);
        inputService.HeldButtons.Remove(MouseButton.Left);
        inputService.HeldButtons.Add(MouseButton.Right);
        system.Tick(1f / 64f, store);
        Assert.True(store.IsDirty<InputComponent>(entity));
        Assert.True(store.TryGet(entity, out InputComponent solvent));
        Assert.True(solvent.SecondaryHeld);
        Assert.Null(solvent.PaintId);
    }

    [Fact]
    public void MovementInputAfterCommandMaxAgeTicksWithNoSnapshotsStillMoves()
    {
        var inputService = new FakeInputService();
        var shortcutService = new ShortcutService(inputService, NullLogger.Instance);
        var windowContext = new FakeWindowContext();
        var physics = new FakePhysics();
        var protocol = new ProtocolConfig();
        var motionProcessor = new ClientPlayerMotionProcessor(
            inputService,
            windowContext,
            physics,
            new EventInvoker<PlayerMovedEvent>([]),
            protocol
        );
        motionProcessor.SetInputEnabled(true);

        var store = new DataStore();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new PlayerComponent());
        store.AddOrUpdate(entity, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(entity, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Discrete));
        store.AddOrUpdate(entity, new InputComponent());
        store.AddOrUpdate(entity, PendingInputComponent.Create());

        var snapshotAck = new SnapshotAckTracker();
        var system = new ClientInputSystem(motionProcessor, shortcutService, new ControlSettings(), snapshotAck);

        //  Bind SimulationStep and advance past CommandMaxAgeTicks (120) with no snapshots arriving.
        motionProcessor.Tick(1f / 64f, store);
        int idleTicks = protocol.CommandMaxAgeTicks.Get() + 30;
        for (var i = 0; i < idleTicks; i++)
        {
            motionProcessor.Step!.Step();
        }

        Assert.Equal(0u, snapshotAck.LastAppliedSnapshotTick);
        Assert.Equal((uint)idleTicks, motionProcessor.Step!.CurrentSimTick);

        inputService.HeldKeys.Add(Key.W);
        system.Tick(1f / 64f, store);

        //  ServerTickAtSample still echoes the last applied snapshot tick, not the local sim tick.
        Assert.True(store.TryGet(entity, out InputComponent sampled));
        Assert.Equal(0u, sampled.ServerTickAtSample);

        motionProcessor.Step.Step();

        Assert.True(store.TryGet(entity, out PhysicsComponent moved));
        Assert.True(moved.Velocity.Length() > 0f, "Fresh movement input after 120 idle ticks must not be treated as stale.");
    }

    private static TestContext CreateContext()
    {
        var inputService = new FakeInputService();
        var shortcutService = new ShortcutService(inputService, NullLogger.Instance);
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

        var system = new ClientInputSystem(motionProcessor, shortcutService, new ControlSettings(), new SnapshotAckTracker());
        return new TestContext(store, entity, system, inputService, shortcutService, motionProcessor);
    }

    private static int PendingCount(TestContext context)
    {
        Assert.True(context.Store.TryGet(context.Entity, out PendingInputComponent pending));
        return pending.Ring!.Count;
    }

    private sealed class TestContext(
        DataStore store,
        int entity,
        ClientInputSystem system,
        FakeInputService inputService,
        ShortcutService shortcutService,
        ClientPlayerMotionProcessor motionProcessor
    ) {
        public DataStore Store { get; } = store;
        public int Entity { get; } = entity;
        public ClientInputSystem System { get; } = system;
        public FakeInputService InputService { get; } = inputService;
        public ShortcutService ShortcutService { get; } = shortcutService;
        public ClientPlayerMotionProcessor MotionProcessor { get; } = motionProcessor;
    }
}
