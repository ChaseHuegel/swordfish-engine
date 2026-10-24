#nullable enable
using Swordfish.ECS;
using Xunit;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Server.Systems;

namespace Swordfish.Tests;

/// <summary>
/// Pins the per-session sequence watermarks and the inventory consume path that reads them, so a
/// reused player mirror cannot inherit a previous session's high-water mark.
/// </summary>
public class NetworkComponentTests
{
    [Fact]
    public void CreateResetsTheConsumedSequenceWatermarks()
    {
        NetworkComponent net = NetworkComponent.Create();
        net.LastConsumedInteractionSequence = 42;
        net.LastConsumedInventorySequence = 7;

        NetworkComponent fresh = NetworkComponent.Create();

        Assert.Equal(0u, fresh.LastConsumedInteractionSequence);
        Assert.Equal(0u, fresh.LastConsumedInventorySequence);
    }

    [Fact]
    public void CreateStagesIndependentRings()
    {
        NetworkComponent first = NetworkComponent.Create();
        NetworkComponent second = NetworkComponent.Create();

        Assert.NotSame(first.StagedInputs, second.StagedInputs);
        Assert.NotSame(first.StagedInteractions, second.StagedInteractions);
        Assert.NotSame(first.StagedInventoryOps, second.StagedInventoryOps);
    }

    [Fact]
    public void ConsumesALowSequenceAgainAfterTheSessionResets()
    {
        var store = new DataStore();
        var system = new ServerInventorySystem();

        int entity = store.Alloc();
        store.AddOrUpdate(entity, StackedInventory());
        store.AddOrUpdate(entity, SessionWithMove(sequence: 5));

        system.Tick(1f, store);

        Assert.True(store.TryGet(entity, out NetworkComponent net));
        Assert.Equal(5u, net.LastConsumedInventorySequence);

        //  A rejoin replaces the session on the same mirror index and restarts the client sequence.
        store.AddOrUpdate(entity, StackedInventory());
        store.AddOrUpdate(entity, SessionWithMove(sequence: 1));

        system.Tick(1f, store);

        Assert.True(store.TryGet(entity, out InventoryComponent inventory));
        Assert.Equal("rock", inventory.Contents![1].ID);
    }

    [Fact]
    public void IgnoresASequenceAtOrBelowTheWatermark()
    {
        var store = new DataStore();
        var system = new ServerInventorySystem();

        int entity = store.Alloc();
        store.AddOrUpdate(entity, StackedInventory());
        store.AddOrUpdate(entity, SessionWithMove(sequence: 5));
        system.Tick(1f, store);

        //  Re-stage the same sequence on the live session: the watermark must still filter it.
        store.AddOrUpdate(entity, StackedInventory());
        StageMove(store, entity, sequence: 5);
        system.Tick(1f, store);

        Assert.True(store.TryGet(entity, out InventoryComponent inventory));
        Assert.Equal("rock", inventory.Contents![0].ID);
        Assert.True(inventory.Contents[1].IsEmpty());
    }

    private static void StageMove(DataStore store, int entity, uint sequence)
    {
        Assert.True(store.TryGet(entity, out NetworkComponent net));

        var op = new SlotMoveOp
        {
            Mode = SlotMoveOpMode.Exact,
            FromSlot = 0,
            ToSlot = 1,
        };
        net.StagedInventoryOps!.Stage(sequence, in op);
    }

    private static InventoryComponent StackedInventory()
    {
        var inventory = new InventoryComponent(2);
        inventory.Contents![0] = new ItemData("rock", 1, 10);
        return inventory;
    }

    private static NetworkComponent SessionWithMove(uint sequence)
    {
        NetworkComponent net = NetworkComponent.Create();
        var op = new SlotMoveOp
        {
            Mode = SlotMoveOpMode.Exact,
            FromSlot = 0,
            ToSlot = 1,
        };
        net.StagedInventoryOps!.Stage(sequence, in op);
        return net;
    }
}
