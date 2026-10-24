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

    [Fact]
    public void ResetSessionClearsLookTotalsAndWritesTheFirstSample()
    {
        TestContext context = CreateContext();

        context.MotionProcessor.CursorUpdates.Enqueue(new Vector2(10f, 0f));
        context.System.Tick(1f / 64f, context.Store);
        Assert.True(context.Store.TryGet(context.Entity, out InputComponent before));
        Assert.NotEqual(0f, before.LookYaw);

        context.Store.ClearDirty<InputComponent>(context.Entity);
        context.System.ResetSession(context.Store.GetUuid(context.Entity));
        context.System.Tick(1f / 64f, context.Store);

        Assert.True(context.Store.TryGet(context.Entity, out InputComponent after));
        Assert.Equal(0f, after.LookPitch);
        Assert.Equal(0f, after.LookYaw);
        Assert.Equal(0f, after.LookRoll);
        Assert.True(context.Store.IsDirty<InputComponent>(context.Entity), "The reset must force a first sample.");
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
        return new TestContext(store, entity, system, inputService, motionProcessor);
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
        ClientPlayerMotionProcessor motionProcessor
    ) {
        public DataStore Store { get; } = store;
        public int Entity { get; } = entity;
        public ClientInputSystem System { get; } = system;
        public FakeInputService InputService { get; } = inputService;
        public ClientPlayerMotionProcessor MotionProcessor { get; } = motionProcessor;
    }
}
