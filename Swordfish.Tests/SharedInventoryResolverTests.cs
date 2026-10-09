using WaywardBeyond.Gameplay;
using WaywardBeyond.Networking.Components;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// The shared inventory move resolver bears the client-prediction/server-authority contract: the same op
/// applied to the same state yields the same result on both sides, and invalid ops are total no-ops.
/// </summary>
public class SharedInventoryResolverTests
{
    private static SlotMoveOp Exact(int fromSlot, int toSlot, uint? count = null)
    {
        return new SlotMoveOp { Mode = SlotMoveOp.MODE_EXACT, FromSlot = fromSlot, ToSlot = toSlot, Count = count };
    }

    [Fact]
    public void WholeStackMovePlacesIntoEmptySlot()
    {
        InventoryComponent inventory = new(10);
        inventory.Contents[0] = InventoryComponent.Stack("glass", 10, 10);

        Assert.True(SharedInventoryResolver.Apply(ref inventory, Exact(0, 5, null)));

        Assert.Equal(10, inventory.Contents[5].Count);
        Assert.Equal("glass", inventory.Contents[5].ID);
        Assert.True(inventory.Contents[0].Count <= 0);
    }

    [Fact]
    public void PartialMoveMovesExactCount()
    {
        InventoryComponent inventory = new(10);
        inventory.Contents[0] = InventoryComponent.Stack("glass", 10, 10);

        Assert.True(SharedInventoryResolver.Apply(ref inventory, Exact(0, 5, 3)));

        Assert.Equal(3, inventory.Contents[5].Count);
        Assert.Equal(7, inventory.Contents[0].Count);
    }

    [Fact]
    public void SameItemStacksUpToDestinationCapacity()
    {
        InventoryComponent inventory = new(10);
        inventory.Contents[0] = InventoryComponent.Stack("glass", 10, 10);
        inventory.Contents[5] = InventoryComponent.Stack("glass", 2, 10);

        Assert.True(SharedInventoryResolver.Apply(ref inventory, Exact(0, 5, null)));

        Assert.Equal(10, inventory.Contents[5].Count);
        Assert.Equal(2, inventory.Contents[0].Count);
    }

    [Fact]
    public void DifferentItemWholeMoveSwapsSlots()
    {
        InventoryComponent inventory = new(10);
        inventory.Contents[0] = InventoryComponent.Stack("glass", 10, 10);
        inventory.Contents[5] = InventoryComponent.Stack("dirt", 4, 10);

        Assert.True(SharedInventoryResolver.Apply(ref inventory, Exact(0, 5, null)));

        Assert.Equal("dirt", inventory.Contents[0].ID);
        Assert.Equal(4, inventory.Contents[0].Count);
        Assert.Equal("glass", inventory.Contents[5].ID);
        Assert.Equal(10, inventory.Contents[5].Count);
    }

    [Fact]
    public void PartialMoveOntoDifferentItemIsNoOp()
    {
        InventoryComponent inventory = new(10);
        inventory.Contents[0] = InventoryComponent.Stack("glass", 10, 10);
        inventory.Contents[5] = InventoryComponent.Stack("dirt", 4, 10);

        Assert.False(SharedInventoryResolver.Apply(ref inventory, Exact(0, 5, 1)));

        Assert.Equal(10, inventory.Contents[0].Count);
        Assert.Equal("dirt", inventory.Contents[5].ID);
    }

    [Fact]
    public void OutOfBoundsSlotsAreRejected()
    {
        InventoryComponent inventory = new(10);
        inventory.Contents[0] = InventoryComponent.Stack("glass", 10, 10);

        Assert.False(SharedInventoryResolver.Apply(ref inventory, Exact(99, 1, null)));
        Assert.False(SharedInventoryResolver.Apply(ref inventory, Exact(0, 99, null)));
        Assert.False(SharedInventoryResolver.Apply(ref inventory, Exact(-1, 1, null)));

        Assert.Equal(10, inventory.Contents[0].Count);
    }

    [Fact]
    public void EmptySourceIsRejected()
    {
        InventoryComponent inventory = new(10);

        Assert.False(SharedInventoryResolver.Apply(ref inventory, Exact(0, 5, null)));
    }

    [Fact]
    public void AutoStackMovesToFirstSameItemStackThenFirstEmpty()
    {
        InventoryComponent inventory = new(20);
        inventory.Contents[0] = InventoryComponent.Stack("glass", 10, 10);
        inventory.Contents[9] = InventoryComponent.Stack("glass", 2, 10);

        var op = new SlotMoveOp { Mode = SlotMoveOp.MODE_AUTO_STACK, FromSlot = 0, ToSlot = -1, Count = null };
        Assert.True(SharedInventoryResolver.Apply(ref inventory, op));

        Assert.Equal(10, inventory.Contents[9].Count);
        Assert.Equal(2, inventory.Contents[0].Count);

        //  The remainder moves to the first empty slot in the target region (no same-item capacity left).
        Assert.True(SharedInventoryResolver.Apply(ref inventory, op));
        Assert.Equal(2, inventory.Contents[10].Count);
        Assert.True(inventory.Contents[0].Count <= 0);
    }

    [Fact]
    public void AutoStackCrossesRegionsLikeQuickMove()
    {
        InventoryComponent inventory = new(20);
        inventory.Contents[0] = InventoryComponent.Stack("dirt", 4, 10);
        inventory.Contents[13] = InventoryComponent.Stack("dirt", 2, 10);

        //  Hotbar slot 0 quick-moves into the inventory region (9+): stacks first.
        var op = new SlotMoveOp { Mode = SlotMoveOp.MODE_AUTO_STACK, FromSlot = 0, ToSlot = -1, Count = null };
        Assert.True(SharedInventoryResolver.Apply(ref inventory, op));
        Assert.Equal(6, inventory.Contents[13].Count);
        Assert.True(inventory.Contents[0].Count <= 0);

        //  An inventory slot quick-moves back into the hotbar region (0-8): first empty slot.
        inventory.Contents[15] = InventoryComponent.Stack("glass", 3, 10);
        var back = new SlotMoveOp { Mode = SlotMoveOp.MODE_AUTO_STACK, FromSlot = 15, ToSlot = -1, Count = null };
        Assert.True(SharedInventoryResolver.Apply(ref inventory, back));
        Assert.Equal(3, inventory.Contents[0].Count);
        Assert.True(inventory.Contents[15].Count <= 0);
    }

    [Fact]
    public void FindFirstEmptySlotSkipsOccupiedSlots()
    {
        InventoryComponent inventory = new(10);
        inventory.Contents[2] = InventoryComponent.Stack("glass", 10, 10);

        Assert.Equal(0, SharedInventoryResolver.FindFirstEmptySlot(in inventory));
        Assert.Equal(3, SharedInventoryResolver.FindFirstEmptySlot(in inventory, 3));
    }
}