using System;
using System.Collections.Generic;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Systems;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Transport;
using NUnit.Framework;

namespace WaywardBeyond.Client.Core.Tests;

/// <summary>
/// Outbound interaction edges must be cleared only after their containing snapshot is sent: with a
/// failing transport the edges stay staged and are re-emitted exactly once on the next successful tick,
/// never duplicated and never lost.
/// </summary>
public class ClientReplicationInteractionTests
{
    private sealed class FlakyConnection : IClientConnection
    {
        public bool IsConnected => true;
        public bool IsLocal => false;
        public bool SendsFail { get; set; }
        public int SnapshotsSent { get; private set; }

        public readonly List<WorldSnapshot> Received = [];

        public Result Send<T>(in T message)
        {
            if (SendsFail)
            {
                return Result.FromFailure("Transport unavailable.");
            }

            if (typeof(T) == typeof(WorldSnapshot))
            {
                Received.Add((WorldSnapshot)(object)message!);
                SnapshotsSent++;
            }

            return Result.FromSuccess();
        }

        public Result<T> Receive<T>()
        {
            return Result<T>.FromFailure("No messages available.");
        }
    }

    [SetUp]
    public void SetUp()
    {
        NetworkRegistry.Initialize([typeof(InputComponent).Assembly]);
    }

    [Test]
    public void FailedSendRetainsStagedEdgesAndDeliversExactlyOnce()
    {
        var connection = new FlakyConnection();
        var system = new ClientReplicationSystem(connection);
        var store = new DataStore();

        int entity = store.Alloc();
        var pending = new PendingInteractionComponent(new PendingInteractionQueue());
        pending.Outbound.Stage(new InteractionEvent
        {
            Entity = store.GetUuid(entity).ToValue(),
            SequenceNumber = 1,
            ServerTickAtSample = 1,
            Kind = (byte)InteractionKind.PrimaryPressed,
        });
        store.AddOrUpdate(entity, pending);

        //  Tick 1: the transport fails; the edge must stay staged.
        connection.SendsFail = true;
        system.Tick(0f, store);
        Assert.That(connection.SnapshotsSent, Is.Zero);
        store.TryGet(entity, out pending);
        Assert.That(pending.Outbound.Snapshot(), Has.Length.EqualTo(1), "A failed send must retain the staged edge.");

        //  Tick 2: the transport recovers; the edge goes out exactly once and the buffer clears.
        connection.SendsFail = false;
        system.Tick(0f, store);
        Assert.That(connection.SnapshotsSent, Is.EqualTo(1));
        Assert.That(connection.Received[0].Components, Has.Length.EqualTo(1));
        store.TryGet(entity, out pending);
        Assert.That(pending.Outbound.Snapshot(), Is.Empty, "A successful send clears the outbound buffer.");

        //  Tick 3: nothing left to send; no duplicate edge ever went out.
        system.Tick(0f, store);
        Assert.That(connection.SnapshotsSent, Is.EqualTo(1));
    }

    [Test]
    public void InventoryOpsDrainIntoSnapshotsAndClearOnSuccess()
    {
        var connection = new FlakyConnection();
        var system = new ClientReplicationSystem(connection);
        var store = new DataStore();

        int entity = store.Alloc();
        store.AddOrUpdate(entity, new PendingInventoryComponent());
        store.QueryRef<PendingInventoryComponent>(entity, 0f,
            (float _, DataStore s, int e, ref Ref<PendingInventoryComponent> pending) =>
            {
                ref PendingInventoryComponent value = ref pending.Write;
                value.Outbound.Stage(++value.NextSequence, new SlotMoveOp { Mode = SlotMoveOp.MODE_EXACT, FromSlot = 7, ToSlot = 0, Count = null });
                value.Outbound.Stage(++value.NextSequence, new SlotMoveOp { Mode = SlotMoveOp.MODE_AUTO_STACK, FromSlot = 9, ToSlot = -1, Count = null });
            });

        //  A failed send retains both ops.
        connection.SendsFail = true;
        system.Tick(0f, store);
        Assert.That(connection.SnapshotsSent, Is.Zero);
        store.TryGet(entity, out PendingInventoryComponent pending);
        Assert.That(pending.Outbound.Snapshot(), Has.Length.EqualTo(2));

        //  A successful send emits one InventoryEvent snapshot per op and clears the buffer.
        connection.SendsFail = false;
        system.Tick(0f, store);
        Assert.That(connection.Received[0].Components, Has.Length.EqualTo(2));
        store.TryGet(entity, out pending);
        Assert.That(pending.Outbound.Snapshot(), Is.Empty);

        system.Tick(0f, store);
        Assert.That(connection.SnapshotsSent, Is.EqualTo(1));
    }
}