#nullable enable
using Swordfish.Library.Util;
using Xunit;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;

namespace WaywardBeyond.Tests;

/// <summary>Pins the shared keyed transfer resolver that client prediction and server authority both run.</summary>
public class InventoryResolverTests
{
    private static readonly SlotId Hotbar0 = new(SlotGroups.HOTBAR, 0);
    private static readonly SlotId Hotbar1 = new(SlotGroups.HOTBAR, 1);
    private static readonly SlotId Hotbar2 = new(SlotGroups.HOTBAR, 2);
    private static readonly SlotId Hotbar3 = new(SlotGroups.HOTBAR, 3);
    private static readonly SlotId Hotbar4 = new(SlotGroups.HOTBAR, 4);
    private static readonly SlotId Hotbar5 = new(SlotGroups.HOTBAR, 5);
    private static readonly SlotId Backpack0 = new(SlotGroups.BACKPACK, 0);
    private static readonly SlotId Backpack3 = new(SlotGroups.BACKPACK, 3);

    [Fact]
    public void ClampsAWholeStackWhenTheWireCountExceedsIt()
    {
        InventoryComponent inventory = Inventory(2);
        inventory.Set(in Hotbar0, new ItemData("rock", 3, 10));

        TransferOp op = Exact(in Hotbar0, in Hotbar1, count: uint.MaxValue);
        Assert.True(InventoryResolver.Apply(ref inventory, in op));

        Assert.Equal(3, inventory.Get(in Hotbar1).Count);
        Assert.True(inventory.Get(in Hotbar0).IsEmpty());
    }

    [Fact]
    public void MovesTheRequestedPartialCount()
    {
        InventoryComponent inventory = Inventory(2);
        inventory.Set(in Hotbar0, new ItemData("rock", 5, 10));

        TransferOp op = Exact(in Hotbar0, in Hotbar1, count: 2);
        Assert.True(InventoryResolver.Apply(ref inventory, in op));

        Assert.Equal(2, inventory.Get(in Hotbar1).Count);
        Assert.Equal(3, inventory.Get(in Hotbar0).Count);
    }

    [Fact]
    public void StacksSameItemUpToTheDestinationCapacity()
    {
        InventoryComponent inventory = Inventory(2);
        inventory.Set(in Hotbar0, new ItemData("rock", 4, 10));
        inventory.Set(in Hotbar1, new ItemData("rock", 7, 10));

        TransferOp op = Exact(in Hotbar0, in Hotbar1);
        Assert.True(InventoryResolver.Apply(ref inventory, in op));

        Assert.Equal(10, inventory.Get(in Hotbar1).Count);
        Assert.Equal(1, inventory.Get(in Hotbar0).Count);
    }

    [Fact]
    public void SwapsDifferentItemsForAWholeStackMove()
    {
        InventoryComponent inventory = Inventory(2);
        inventory.Set(in Hotbar0, new ItemData("rock", 1, 10));
        inventory.Set(in Hotbar1, new ItemData("ice", 2, 10));

        TransferOp op = Exact(in Hotbar0, in Hotbar1);
        Assert.True(InventoryResolver.Apply(ref inventory, in op));

        Assert.Equal("ice", inventory.Get(in Hotbar0).ID);
        Assert.Equal("rock", inventory.Get(in Hotbar1).ID);
    }

    [Fact]
    public void RejectsAPartialMoveOntoADifferentItem()
    {
        InventoryComponent inventory = Inventory(2);
        inventory.Set(in Hotbar0, new ItemData("rock", 2, 10));
        inventory.Set(in Hotbar1, new ItemData("ice", 2, 10));

        TransferOp op = Exact(in Hotbar0, in Hotbar1, count: 1);
        Assert.False(InventoryResolver.Apply(ref inventory, in op));

        Assert.Equal("rock", inventory.Get(in Hotbar0).ID);
        Assert.Equal("ice", inventory.Get(in Hotbar1).ID);
    }

    [Fact]
    public void AutoStackMovesIntoTheFirstSameItemWithCapacity()
    {
        InventoryComponent inventory = PlayerInventory();
        inventory.Set(in Hotbar0, new ItemData("rock", 2, 10));
        inventory.Set(in Backpack0, new ItemData("rock", 5, 10));

        TransferOp op = AutoStack(in Hotbar0);
        Assert.True(InventoryResolver.Apply(ref inventory, in op));

        Assert.Equal(7, inventory.Get(in Backpack0).Count);
        Assert.True(inventory.Get(in Hotbar0).IsEmpty());
    }

    [Fact]
    public void AutoStackFallsBackToTheFirstEmptySlot()
    {
        InventoryComponent inventory = PlayerInventory();
        inventory.Set(in Hotbar0, new ItemData("rock", 2, 10));

        TransferOp op = AutoStack(in Hotbar0);
        Assert.True(InventoryResolver.Apply(ref inventory, in op));

        Assert.True(inventory.Get(in Hotbar0).IsEmpty());
        Assert.Equal("rock", inventory.Get(in Backpack0).ID);
    }

    [Fact]
    public void AutoStackMovesAnInventorySlotIntoTheHotbar()
    {
        InventoryComponent inventory = PlayerInventory();
        inventory.Set(in Backpack3, new ItemData("rock", 2, 10));

        TransferOp op = AutoStack(in Backpack3);
        Assert.True(InventoryResolver.Apply(ref inventory, in op));

        Assert.Equal("rock", inventory.Get(in Hotbar0).ID);
        Assert.True(inventory.Get(in Backpack3).IsEmpty());
    }

    [Fact]
    public void RejectsAnEmptySource()
    {
        InventoryComponent inventory = Inventory(2);

        TransferOp op = Exact(in Hotbar0, in Hotbar1);
        Assert.False(InventoryResolver.Apply(ref inventory, in op));
    }

    [Fact]
    public void RejectsAZeroCount()
    {
        InventoryComponent inventory = Inventory(2);
        inventory.Set(in Hotbar0, new ItemData("rock", 5, 10));

        TransferOp op = Exact(in Hotbar0, in Hotbar1, count: 0);
        Assert.False(InventoryResolver.Apply(ref inventory, in op));
        Assert.Equal(5, inventory.Get(in Hotbar0).Count);
    }

    [Fact]
    public void RejectsOutOfBoundsSlots()
    {
        InventoryComponent inventory = Inventory(2);
        inventory.Set(in Hotbar0, new ItemData("rock", 5, 10));

        var beyond = new SlotId(SlotGroups.HOTBAR, 9);
        TransferOp beyondSource = Exact(in beyond, in Hotbar1);
        Assert.False(InventoryResolver.Apply(ref inventory, in beyondSource));

        TransferOp beyondDestination = Exact(in Hotbar0, in beyond);
        Assert.False(InventoryResolver.Apply(ref inventory, in beyondDestination));
    }

    [Fact]
    public void FindFirstEmptySlotTreatsANegativeStartAsZero()
    {
        InventoryComponent inventory = Inventory(4);

        Result<SlotId> found = InventoryResolver.FindFirstEmptySlot(in inventory, new SlotId(SlotGroups.HOTBAR, -5));
        Assert.True(found.Success);
        Assert.Equal(Hotbar0, found.Value);
    }

    [Fact]
    public void FindFirstEmptySlotSkipsOccupiedSlots()
    {
        InventoryComponent inventory = Inventory(4);
        inventory.Set(in Hotbar0, new ItemData("rock", 5, 10));

        Result<SlotId> first = InventoryResolver.FindFirstEmptySlot(in inventory);
        Assert.True(first.Success);
        Assert.Equal(Hotbar1, first.Value);

        Result<SlotId> fromSlot2 = InventoryResolver.FindFirstEmptySlot(in inventory, in Hotbar2);
        Assert.True(fromSlot2.Success);
        Assert.Equal(Hotbar2, fromSlot2.Value);
    }

    [Fact]
    public void FindFirstEmptySlotDoesNotSpillIntoFilteredGroupsWhenStartingInBackpack()
    {
        InventoryComponent inventory = InventoryComponent.Create(
            new SlotGroupDescriptor(SlotGroups.HOTBAR, 1),
            new SlotGroupDescriptor(SlotGroups.BACKPACK, 1),
            new SlotGroupDescriptor(SlotGroups.WEARABLES, 1),
            new SlotGroupDescriptor(SlotGroups.AUGMENTS, 1));
        inventory.Set(in Hotbar0, new ItemData("rock", 1, 64));
        inventory.Set(in Backpack0, new ItemData("rock", 1, 64));

        Result<SlotId> fromBackpack = InventoryResolver.FindFirstEmptySlot(in inventory, in Backpack0);
        Assert.False(fromBackpack.Success);

        Result<SlotId> explicitWearable = InventoryResolver.FindFirstEmptySlot(in inventory, new SlotId(SlotGroups.WEARABLES, 0));
        Assert.True(explicitWearable.Success);
        Assert.Equal(new SlotId(SlotGroups.WEARABLES, 0), explicitWearable.Value);
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
        inventory.Set(in Hotbar0, new ItemData("rock", 5, 10));

        TransferOp op = Exact(in Hotbar0, in Hotbar0, count: 3);
        Assert.False(InventoryResolver.Apply(ref inventory, in op));
        Assert.Equal(5, inventory.Get(in Hotbar0).Count);
    }

    [Fact]
    public void ComputeDistributionSplitsEvenlyAcrossFourSlots()
    {
        InventoryComponent inventory = Inventory(10);
        inventory.Set(in Hotbar0, new ItemData("rock", 40, 64));

        SlotId[] targets = [Hotbar1, Hotbar2, Hotbar3, Hotbar4];
        var allocations = new int[4];

        int remainder = InventoryResolver.ComputeDistribution(in inventory, in Hotbar0, targets, allocations);

        Assert.Equal(0, remainder);
        Assert.Equal([10, 10, 10, 10], allocations);
    }

    [Fact]
    public void ComputeDistributionKeepsRemainderOnUnevenSplit()
    {
        InventoryComponent inventory = Inventory(10);
        inventory.Set(in Hotbar0, new ItemData("rock", 40, 64));

        SlotId[] targets = [Hotbar1, Hotbar2, Hotbar3];
        var allocations = new int[3];

        int remainder = InventoryResolver.ComputeDistribution(in inventory, in Hotbar0, targets, allocations);

        Assert.Equal(1, remainder);
        Assert.Equal([13, 13, 13], allocations);
    }

    [Fact]
    public void ComputeDistributionClampsToDestinationCapacity()
    {
        InventoryComponent inventory = Inventory(10);
        inventory.Set(in Hotbar0, new ItemData("rock", 40, 64));
        inventory.Set(in Hotbar1, new ItemData("rock", 60, 64));

        SlotId[] targets = [Hotbar1, Hotbar2];
        var allocations = new int[2];

        int remainder = InventoryResolver.ComputeDistribution(in inventory, in Hotbar0, targets, allocations);

        Assert.Equal(16, remainder);
        Assert.Equal([4, 20], allocations);
    }

    [Fact]
    public void ComputeDistributionCapsTargetSlotsAtSourceCount()
    {
        InventoryComponent inventory = Inventory(10);
        inventory.Set(in Hotbar0, new ItemData("rock", 3, 16));

        SlotId[] targets = [Hotbar1, Hotbar2, Hotbar3, Hotbar4, Hotbar5];
        var allocations = new int[5];

        int remainder = InventoryResolver.ComputeDistribution(in inventory, in Hotbar0, targets, allocations);

        Assert.Equal(0, remainder);
        Assert.Equal([1, 1, 1, 0, 0], allocations);
    }

    [Fact]
    public void ComputeDistributionSkipsDuplicatesAndIncompatibleSlots()
    {
        InventoryComponent inventory = Inventory(10);
        inventory.Set(in Hotbar0, new ItemData("rock", 20, 64));
        inventory.Set(in Hotbar2, new ItemData("ice", 5, 64));

        SlotId[] targets = [Hotbar1, Hotbar2, Hotbar1, Hotbar3];
        var allocations = new int[4];

        int remainder = InventoryResolver.ComputeDistribution(in inventory, in Hotbar0, targets, allocations);

        Assert.Equal(0, remainder);
        Assert.Equal([10, 0, 0, 10], allocations);
    }

    [Fact]
    public void CanDistributeToSlotValidatesCompatibility()
    {
        InventoryComponent inventory = Inventory(5);
        inventory.Set(in Hotbar0, new ItemData("rock", 10, 16));
        inventory.Set(in Hotbar1, new ItemData("rock", 12, 16));
        inventory.Set(in Hotbar2, new ItemData("rock", 16, 16));
        inventory.Set(in Hotbar3, new ItemData("ice", 4, 16));

        Assert.True(InventoryResolver.CanDistributeToSlot(in inventory, in Hotbar0, in Hotbar0));
        Assert.True(InventoryResolver.CanDistributeToSlot(in inventory, in Hotbar0, in Hotbar1));
        Assert.False(InventoryResolver.CanDistributeToSlot(in inventory, in Hotbar0, in Hotbar2));
        Assert.False(InventoryResolver.CanDistributeToSlot(in inventory, in Hotbar0, in Hotbar3));
        Assert.True(InventoryResolver.CanDistributeToSlot(in inventory, in Hotbar0, in Hotbar4));
        var outOfBounds = new SlotId(SlotGroups.HOTBAR, 99);
        Assert.False(InventoryResolver.CanDistributeToSlot(in inventory, in Hotbar0, in outOfBounds));
    }

    private static InventoryComponent PlayerInventory()
    {
        return InventoryComponent.CreatePlayer();
    }

    private static InventoryComponent Inventory(int hotbarCapacity)
    {
        return InventoryComponent.Create(new SlotGroupDescriptor(SlotGroups.HOTBAR, hotbarCapacity));
    }

    private static TransferOp Exact(in SlotId from, in SlotId to, uint? count = null)
    {
        return new TransferOp(0UL, from, 0UL, to, TransferMode.Exact, count);
    }

    private static TransferOp AutoStack(in SlotId from)
    {
        return new TransferOp(0UL, from, 0UL, default, TransferMode.AutoStack, null);
    }
}
