#nullable enable
using Xunit;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;

namespace Swordfish.Tests;

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
