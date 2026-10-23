using System;
using Swordfish.ECS;
using Swordfish.Library.Util;

namespace WaywardBeyond.Data.Components;

/// <summary>The server-authoritative inventory of an entity.</summary>
/// <remarks>Mutation off the ECS thread is unsafe.</remarks>
public partial struct InventoryComponent : IDataComponent
{
    /// <summary>Canonical inventory slot count for players. Client build and server seed agree on this.</summary>
    private const int DEFAULT_SLOT_COUNT = 45;

    /// <summary>Creates an empty inventory with the canonical <see cref="DEFAULT_SLOT_COUNT"/> slots.</summary>
    public InventoryComponent() : this(DEFAULT_SLOT_COUNT) { }

    /// <summary>Creates an empty inventory with <paramref name="size"/> slots.</summary>
    public InventoryComponent(int size) : this(new ItemData[size]) { }

    /// <summary>Copies <paramref name="contents"/> into this inventory's slots, bounded to capacity.</summary>
    public void CopyFrom(ItemData[]? contents)
    {
        if (contents == null)
        {
            return;
        }

        int count = Math.Min(contents.Length, Contents.Length);
        for (var i = 0; i < count; i++)
        {
            Contents[i] = contents[i];
        }
    }

    /// <summary>Clamps an inventory slot index into valid bounds.</summary>
    public static int ClampSlot(int slot)
    {
        return Math.Clamp(slot, 0, DEFAULT_SLOT_COUNT - 1);
    }

    /// <summary>
    /// Stacks <paramref name="itemStack"/> into same-item stacks with capacity, then fills the first
    /// empty slot; a remaining overflow repeats from the next slot. When <paramref name="onlyEmptySlots"/>
    /// is set, the first pass skips stacking and only fills empty slots. Returns false when no slot
    /// remains for the leftover items.
    /// </summary>
    public bool Add(ItemData itemStack, int startingSlot = 0, bool onlyEmptySlots = false)
    {
        for (int startSlot = startingSlot; startSlot < Contents.Length && itemStack.Count > 0; startSlot++)
        {
            int firstEmptySlot = -1;
            bool allowStacking = !onlyEmptySlots || startSlot > startingSlot;

            for (int i = startSlot; i < Contents.Length && itemStack.Count > 0; i++)
            {
                ItemData slotStack = Contents[i];
                if (firstEmptySlot == -1 && (slotStack.Count == 0 || string.IsNullOrEmpty(slotStack.ID)))
                {
                    firstEmptySlot = i;
                }

                if (!string.IsNullOrEmpty(slotStack.ID) && slotStack.ID != itemStack.ID)
                {
                    continue;
                }

                if (allowStacking)
                {
                    int available = slotStack.MaxSize - slotStack.Count;
                    int remainder = Math.Max(0, itemStack.Count - available);
                    slotStack.Count += itemStack.Count - remainder;
                    itemStack.Count = remainder;
                    Contents[i] = slotStack;
                }
            }

            if (itemStack.Count <= 0)
            {
                return true;
            }

            if (firstEmptySlot == -1)
            {
                return false;
            }

            //  Fill the first empty slot; a stack over its max size repeats from the next slot.
            int overflow = Math.Max(0, itemStack.Count - itemStack.MaxSize);
            itemStack.Count = Math.Min(itemStack.Count, itemStack.MaxSize);
            Contents[firstEmptySlot] = itemStack;

            if (overflow <= 0)
            {
                return true;
            }

            itemStack.Count = overflow;
        }

        return itemStack.Count <= 0;
    }

    public bool Add(int slot, ItemData itemStack)
    {
        ItemData slotStack = Contents[slot];
        bool isSlotEmpty = string.IsNullOrEmpty(slotStack.ID);
        if (!isSlotEmpty && slotStack.ID != itemStack.ID)
        {
            //  The item can't be stacked in the slot.
            return false;
        }

        if (!isSlotEmpty && slotStack.MaxSize < slotStack.Count + itemStack.Count)
        {
            //  Not enough space to stack the item in the slot.
            return false;
        }

        if (!isSlotEmpty)
        {
            itemStack.Count += slotStack.Count;
        }

        Contents[slot] = itemStack;
        return true;
    }

    public Result<ItemData> Remove(ItemData itemStack)
    {
        for (var i = 0; i < Contents.Length; i++)
        {
            ItemData slotStack = Contents[i];
            if (slotStack.ID != itemStack.ID)
            {
                continue;
            }

            return Remove(i, itemStack.Count);
        }

        return Result<ItemData>.FromFailure("Item not found");
    }

    public Result<ItemData> Remove(int slot, int amount = -1)
    {
        if (slot < 0 || slot >= Contents.Length)
        {
            return Result<ItemData>.FromFailure("Slot is out of bounds");
        }

        ItemData slotStack = Contents[slot];
        if (slotStack.Count <= 0 || string.IsNullOrEmpty(slotStack.ID))
        {
            return Result<ItemData>.FromFailure("Slot is empty");
        }

        if (amount < 0)
        {
            amount = slotStack.Count;
        }
        else
        {
            amount = Math.Min(amount, slotStack.Count);
        }

        slotStack.Count -= amount;
        Contents[slot] = slotStack.Count > 0 ? slotStack : default;

        return Result<ItemData>.FromSuccess(slotStack with { Count = amount });
    }

    public bool Swap(int slot1, int slot2)
    {
        if (slot1 < 0 || slot1 >= Contents.Length || slot2 < 0 || slot2 >= Contents.Length)
        {
            return false;
        }

        (Contents[slot1], Contents[slot2]) = (Contents[slot2], Contents[slot1]);
        return true;
    }
}