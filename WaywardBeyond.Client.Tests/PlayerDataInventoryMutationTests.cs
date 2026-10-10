using System.Collections.Generic;
using Swordfish.ECS;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Items;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Config;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Components;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Networking.Transport;
using NUnit.Framework;

namespace WaywardBeyond.Client.Tests;

/// <summary>
/// The client's single inventory mutation entry point, <see cref="PlayerData.ApplyMove"/>, must both
/// predict the move onto the local server-owned inventory (via the shared resolver) and stage it for
/// upstream replication. A middle-click brick pick sends exactly this whole-stack swap into the active
/// hotbar slot, so the reusable API pinning the prediction-and-stage coupling covers the fix.
/// </summary>
public class PlayerDataInventoryMutationTests
{
    private sealed class StubItemDatabase : IAssetDatabase<Item>
    {
        public Result<Item> Get(string id) => Result<Item>.FromFailure("Not found.");
    }

    private sealed class CapturingConnection : IClientConnection
    {
        public bool IsConnected => true;
        public bool IsLocal => false;
        public readonly List<WorldSnapshot> Received = [];

        public Result Send<T>(in T message)
        {
            if (typeof(T) == typeof(WorldSnapshot))
            {
                Received.Add((WorldSnapshot)(object)message!);
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
    public void ApplyMovePredictsLocallyAndStagesForReplication()
    {
        var store = new DataStore();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new PlayerComponent());

        var inventory = new InventoryComponent();
        inventory.Contents[0] = InventoryComponent.Stack("marker", 1, 100);
        inventory.Contents[9] = InventoryComponent.Stack("wb:rock", 5, 100);
        store.AddOrUpdate(entity, inventory);
        store.AddOrUpdate(entity, new PendingInventoryComponent());

        var playerData = new PlayerData(new StubItemDatabase());
        playerData.ApplyMove(store, new SlotMoveOp
        {
            Mode = SlotMoveOp.MODE_EXACT,
            FromSlot = 9,
            ToSlot = 0,
            Count = null,
        });

        //  The pick swaps the whole stack into the active slot as local prediction.
        Assert.That(store.TryGet(entity, out InventoryComponent predicted), Is.True);
        Assert.That(predicted.Contents[0].ID, Is.EqualTo("wb:rock"), "The picked brick moves into the active slot.");
        Assert.That(predicted.Contents[9].ID, Is.EqualTo("marker"), "The occupied destination swaps out.");

        //  The same op is staged upstream for the server's authoritative apply.
        Assert.That(store.TryGet(entity, out PendingInventoryComponent pending), Is.True);
        InventoryOpStageBuffer.InventoryOp[] ops = pending.Outbound.Snapshot();
        Assert.That(ops, Has.Length.EqualTo(1));
        Assert.That(ops[0].SequenceNumber, Is.EqualTo(1));
        Assert.That(ops[0].SlotMove.FromSlot, Is.EqualTo(9));
        Assert.That(ops[0].SlotMove.ToSlot, Is.EqualTo(0));
        Assert.That(ops[0].SlotMove.Count, Is.Null, "A whole-stack pick carries no explicit count.");
    }

    [Test]
    public void StagedPickSwapEmitsInventoryEventOnReplicationTick()
    {
        var store = new DataStore();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new PlayerComponent());

        var inventory = new InventoryComponent();
        inventory.Contents[0] = InventoryComponent.Stack("marker", 1, 100);
        inventory.Contents[9] = InventoryComponent.Stack("wb:rock", 5, 100);
        store.AddOrUpdate(entity, inventory);
        store.AddOrUpdate(entity, new PendingInventoryComponent());

        var playerData = new PlayerData(new StubItemDatabase());
        playerData.ApplyMove(store, new SlotMoveOp
        {
            Mode = SlotMoveOp.MODE_EXACT,
            FromSlot = 9,
            ToSlot = 0,
            Count = null,
        });

        //  The staged pick rides the next replication snapshot as an InventoryEvent and clears.
        var connection = new CapturingConnection();
        var system = new ClientReplicationSystem(connection, new NetworkingConfig());
        system.Tick(1f, store);

        Assert.That(connection.Received, Has.Count.EqualTo(1));
        Assert.That(connection.Received[0].Components, Has.Length.EqualTo(1));
        Assert.That(store.TryGet(entity, out PendingInventoryComponent pending), Is.True);
        Assert.That(pending.Outbound.Snapshot(), Is.Empty, "A successful send clears the staged op.");
    }
}