#nullable enable
using Xunit;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;

namespace WaywardBeyond.Tests;

/// <summary>Pins the shared slot-move resolver that client prediction and server authority both run.</summary>
public class InventoryResolverTests
{
    [Fact]
    public void ClampsAWholeStackWhenTheWireCountExceedsIt()
    {
        InventoryComponent inventory = Inventory(2);
        inventory.Contents![0] = new ItemData("rock", 3, 10);

        var op = Exact(from: 0, to: 1, count: uint.MaxValue);
        Assert.True(InventoryResolver.Apply(ref inventory, in op));

        Assert.Equal(3, inventory.Contents[1].Count);
        Assert.True(IsEmpty(inventory.Contents[0]));
    }

    [Fact]
    public void MovesTheRequestedPartialCount()
    {
        InventoryComponent inventory = Inventory(2);
        inventory.Contents![0] = new ItemData("rock", 5, 10);

        var op = Exact(from: 0, to: 1, count: 2);
        Assert.True(InventoryResolver.Apply(ref inventory, in op));

        Assert.Equal(2, inventory.Contents[1].Count);
        Assert.Equal(3, inventory.Contents[0].Count);
    }

    [Fact]
    public void StacksSameItemUpToTheDestinationCapacity()
    {
        InventoryComponent inventory = Inventory(2);
        inventory.Contents![0] = new ItemData("rock", 4, 10);
        inventory.Contents[1] = new ItemData("rock", 7, 10);

        var op = Exact(from: 0, to: 1);
        Assert.True(InventoryResolver.Apply(ref inventory, in op));

        Assert.Equal(10, inventory.Contents[1].Count);
        Assert.Equal(1, inventory.Contents[0].Count);
    }

    [Fact]
    public void SwapsDifferentItemsForAWholeStackMove()
    {
        InventoryComponent inventory = Inventory(2);
        inventory.Contents![0] = new ItemData("rock", 1, 10);
        inventory.Contents[1] = new ItemData("ice", 2, 10);

        var op = Exact(from: 0, to: 1);
        Assert.True(InventoryResolver.Apply(ref inventory, in op));

        Assert.Equal("ice", inventory.Contents[0].ID);
        Assert.Equal("rock", inventory.Contents[1].ID);
    }

    [Fact]
    public void RejectsAPartialMoveOntoADifferentItem()
    {
        InventoryComponent inventory = Inventory(2);
        inventory.Contents![0] = new ItemData("rock", 2, 10);
        inventory.Contents[1] = new ItemData("ice", 2, 10);

        var op = Exact(from: 0, to: 1, count: 1);
        Assert.False(InventoryResolver.Apply(ref inventory, in op));

        Assert.Equal("rock", inventory.Contents[0].ID);
        Assert.Equal("ice", inventory.Contents[1].ID);
    }

    [Fact]
    public void AutoStackMovesIntoTheFirstSameItemWithCapacity()
    {
        InventoryComponent inventory = Inventory();
        inventory.Contents![0] = new ItemData("rock", 2, 10);
        inventory.Contents[CharacterDefaults.HOTBAR_SLOT_COUNT] = new ItemData("rock", 5, 10);

        var op = AutoStack(from: 0);
        Assert.True(InventoryResolver.Apply(ref inventory, in op));

        Assert.Equal(7, inventory.Contents[CharacterDefaults.HOTBAR_SLOT_COUNT].Count);
        Assert.True(IsEmpty(inventory.Contents[0]));
    }

    [Fact]
    public void AutoStackFallsBackToTheFirstEmptySlot()
    {
        InventoryComponent inventory = Inventory();
        inventory.Contents![0] = new ItemData("rock", 2, 10);

        var op = AutoStack(from: 0);
        Assert.True(InventoryResolver.Apply(ref inventory, in op));

        Assert.True(IsEmpty(inventory.Contents[0]));
        Assert.Equal("rock", inventory.Contents[CharacterDefaults.HOTBAR_SLOT_COUNT].ID);
    }

    [Fact]
    public void AutoStackMovesAnInventorySlotIntoTheHotbar()
    {
        InventoryComponent inventory = Inventory();
        int source = CharacterDefaults.HOTBAR_SLOT_COUNT + 3;
        inventory.Contents![source] = new ItemData("rock", 2, 10);

        var op = AutoStack(from: source);
        Assert.True(InventoryResolver.Apply(ref inventory, in op));

        Assert.Equal("rock", inventory.Contents[0].ID);
        Assert.True(IsEmpty(inventory.Contents[source]));
    }

    [Fact]
    public void RejectsAnEmptySource()
    {
        InventoryComponent inventory = Inventory(2);

        var op = Exact(from: 0, to: 1);
        Assert.False(InventoryResolver.Apply(ref inventory, in op));
    }

    [Fact]
    public void RejectsAZeroCount()
    {
        InventoryComponent inventory = Inventory(2);
        inventory.Contents![0] = new ItemData("rock", 5, 10);

        var op = Exact(from: 0, to: 1, count: 0);
        Assert.False(InventoryResolver.Apply(ref inventory, in op));
        Assert.Equal(5, inventory.Contents[0].Count);
    }

    [Fact]
    public void RejectsOutOfBoundsSlots()
    {
        InventoryComponent inventory = Inventory(2);
        inventory.Contents![0] = new ItemData("rock", 5, 10);

        var beyondSource = Exact(from: 9, to: 1);
        Assert.False(InventoryResolver.Apply(ref inventory, in beyondSource));

        var beyondDestination = Exact(from: 0, to: 9);
        Assert.False(InventoryResolver.Apply(ref inventory, in beyondDestination));
    }

    [Fact]
    public void FindFirstEmptySlotTreatsANegativeStartAsZero()
    {
        InventoryComponent inventory = Inventory(4);

        Assert.Equal(0, InventoryResolver.FindFirstEmptySlot(inventory, startingSlot: -5));
    }

    [Fact]
    public void FindFirstEmptySlotSkipsOccupiedSlots()
    {
        InventoryComponent inventory = Inventory(4);
        inventory.Contents![0] = new ItemData("rock", 5, 10);

        Assert.Equal(1, InventoryResolver.FindFirstEmptySlot(inventory));
        Assert.Equal(2, InventoryResolver.FindFirstEmptySlot(inventory, startingSlot: 2));
    }

    [Fact]
    public void ReadsASlotWithANonPositiveCountAsEmpty()
    {
        Assert.True(new ItemData("rock", -1, 10).IsEmpty());
        Assert.True(default(ItemData).IsEmpty());
        Assert.False(new ItemData("rock", 1, 10).IsEmpty());
    }

    [Fact]
    public void RejectsSameSlotExactMove()
    {
        InventoryComponent inventory = Inventory(2);
        inventory.Contents![0] = new ItemData("rock", 5, 10);

        var op = Exact(from: 0, to: 0, count: 3);
        Assert.False(InventoryResolver.Apply(ref inventory, in op));
        Assert.Equal(5, inventory.Contents[0].Count);
    }

    [Fact]
    public void ComputeDistributionSplitsEvenlyAcrossFourSlots()
    {
        InventoryComponent inventory = Inventory(10);
        inventory.Contents![0] = new ItemData("rock", 40, 64);

        int[] targets = [1, 2, 3, 4];
        var allocations = new int[4];

        int remainder = InventoryResolver.ComputeDistribution(in inventory, 0, targets, allocations);

        Assert.Equal(0, remainder);
        Assert.Equal([10, 10, 10, 10], allocations);
    }

    [Fact]
    public void ComputeDistributionKeepsRemainderOnUnevenSplit()
    {
        InventoryComponent inventory = Inventory(10);
        inventory.Contents![0] = new ItemData("rock", 40, 64);

        int[] targets = [1, 2, 3];
        var allocations = new int[3];

        int remainder = InventoryResolver.ComputeDistribution(in inventory, 0, targets, allocations);

        Assert.Equal(1, remainder);
        Assert.Equal([13, 13, 13], allocations);
    }

    [Fact]
    public void ComputeDistributionClampsToDestinationCapacity()
    {
        InventoryComponent inventory = Inventory(10);
        inventory.Contents![0] = new ItemData("rock", 40, 64);
        inventory.Contents[1] = new ItemData("rock", 60, 64);

        int[] targets = [1, 2];
        var allocations = new int[2];

        int remainder = InventoryResolver.ComputeDistribution(in inventory, 0, targets, allocations);

        Assert.Equal(16, remainder);
        Assert.Equal([4, 20], allocations);
    }

    [Fact]
    public void ComputeDistributionCapsTargetSlotsAtSourceCount()
    {
        InventoryComponent inventory = Inventory(10);
        inventory.Contents![0] = new ItemData("rock", 3, 16);

        int[] targets = [1, 2, 3, 4, 5];
        var allocations = new int[5];

        int remainder = InventoryResolver.ComputeDistribution(in inventory, 0, targets, allocations);

        Assert.Equal(0, remainder);
        Assert.Equal([1, 1, 1, 0, 0], allocations);
    }

    [Fact]
    public void ComputeDistributionSkipsDuplicatesAndIncompatibleSlots()
    {
        InventoryComponent inventory = Inventory(10);
        inventory.Contents![0] = new ItemData("rock", 20, 64);
        inventory.Contents[2] = new ItemData("ice", 5, 64);

        int[] targets = [1, 2, 1, 3];
        var allocations = new int[4];

        int remainder = InventoryResolver.ComputeDistribution(in inventory, 0, targets, allocations);

        Assert.Equal(0, remainder);
        Assert.Equal([10, 0, 0, 10], allocations);
    }

    [Fact]
    public void CanDistributeToSlotValidatesCompatibility()
    {
        InventoryComponent inventory = Inventory(5);
        inventory.Contents![0] = new ItemData("rock", 10, 16);
        inventory.Contents[1] = new ItemData("rock", 12, 16);
        inventory.Contents[2] = new ItemData("rock", 16, 16);
        inventory.Contents[3] = new ItemData("ice", 4, 16);

        Assert.True(InventoryResolver.CanDistributeToSlot(in inventory, 0, 0));
        Assert.True(InventoryResolver.CanDistributeToSlot(in inventory, 0, 1));
        Assert.False(InventoryResolver.CanDistributeToSlot(in inventory, 0, 2));
        Assert.False(InventoryResolver.CanDistributeToSlot(in inventory, 0, 3));
        Assert.True(InventoryResolver.CanDistributeToSlot(in inventory, 0, 4));
        Assert.False(InventoryResolver.CanDistributeToSlot(in inventory, 0, 99));
    }

    private static InventoryComponent Inventory(int size = 45)
    {
        return new InventoryComponent(size);
    }

    private static SlotMoveOp Exact(int from, int to, uint? count = null)
    {
        return new SlotMoveOp
        {
            Mode = SlotMoveOpMode.Exact,
            FromSlot = from,
            ToSlot = to,
            Count = count,
        };
    }

    private static SlotMoveOp AutoStack(int from)
    {
        return new SlotMoveOp
        {
            Mode = SlotMoveOpMode.AutoStack,
            FromSlot = from,
            ToSlot = -1,
        };
    }

    private static bool IsEmpty(in ItemData item)
    {
        return item.Count <= 0 || string.IsNullOrEmpty(item.ID);
    }
}
