using System;
using System.Threading;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking.Registry;

namespace WaywardBeyond.Shared.Networking.Components;

/// <summary>
/// The server-authoritative inventory of a player, replicated downstream as <see cref="ItemData"/>
/// stacks. Authored by the server after the join-time seed; the client predicts presentably against it
/// but does not author it. The stacking/mutation logic lives here (shared) so both the client's
/// presentation and the server's authority use identical behavior.
/// </summary>
[NetworkComponent(13, NetworkDirection.ServerOwned)]
public partial struct InventoryComponent : IDataComponent
{
    private readonly object _lock = new();

    /// <summary>Creates an empty inventory with <paramref name="size"/> slots.</summary>
    public InventoryComponent(int size) : this(new ItemData[size]) { }

    /// <summary>Creates a <see cref="ItemData"/> stack.</summary>
    public static ItemData Stack(string id, int count, int maxSize)
    {
        return new ItemData(id, count, maxSize);
    }

    /// <summary>Creates a <see cref="ItemData"/> stack with a single item.</summary>
    public static ItemData Stack(string id, int maxSize)
    {
        return new ItemData(id, 1, maxSize);
    }

    public bool Add(ItemData itemStack, int startingSlot = 0, bool onlyEmptySlots = false)
    {
        lock (_lock)
        {
            for (int startSlot = startingSlot; startSlot < Contents.Length; startSlot++)
            {
                int firstEmptySlot = -1;
                for (int i = startSlot; i < Contents.Length; i++)
                {
                    if (itemStack.Count <= 0)
                    {
                        //  The stack has been consumed, or was empty to begin with.
                        return true;
                    }

                    ItemData slotItemStack = Contents[i];

                    if (firstEmptySlot == -1 && (slotItemStack.Count == 0 || string.IsNullOrEmpty(slotItemStack.ID)))
                    {
                        firstEmptySlot = i;
                    }

                    if (!string.IsNullOrEmpty(slotItemStack.ID) && slotItemStack.ID != itemStack.ID)
                    {
                        continue;
                    }

                    if (onlyEmptySlots && startSlot == 0)
                    {
                        //  If an empty slot is desired, then don't try to stack
                        //  in the first iteration. Look for an empty slot.
                        continue;
                    }

                    int available = slotItemStack.MaxSize - slotItemStack.Count;
                    int remainder = Math.Max(0, itemStack.Count - available);

                    slotItemStack.Count += itemStack.Count - remainder;
                    itemStack.Count = remainder;

                    Contents[i] = slotItemStack;
                }

                if (itemStack.Count <= 0)
                {
                    //  The stack has been consumed, or was empty to begin with.
                    return true;
                }

                if (firstEmptySlot == -1)
                {
                    //  There is no slot to place the remaining stack into.
                    return false;
                }

                //  Fill the first empty slot.
                int overflowAmount = itemStack.Count - itemStack.MaxSize;

                itemStack.Count = Math.Min(itemStack.Count, itemStack.MaxSize);
                Contents[firstEmptySlot] = itemStack;

                if (overflowAmount <= 0)
                {
                    //  The stack has been consumed.
                    return true;
                }

                //  There is remaining items to distribute
                itemStack.Count = overflowAmount;
            }

            return itemStack.Count <= 0;
        }
    }

    public bool Add(int slot, ItemData itemStack)
    {
        lock (_lock)
        {
            ItemData slotItemStack = Contents[slot];
            bool isSlotEmpty = string.IsNullOrEmpty(slotItemStack.ID);
            if (!isSlotEmpty && slotItemStack.ID != itemStack.ID)
            {
                //  The item can't be stacked in the slot.
                return false;
            }

            if (!isSlotEmpty && slotItemStack.MaxSize < slotItemStack.Count + itemStack.Count)
            {
                //  Not enough space to stack the item in the slot.
                return false;
            }

            if (!isSlotEmpty)
            {
                itemStack.Count += slotItemStack.Count;
            }

            Contents[slot] = itemStack;
            return true;
        }
    }

    public Result<ItemData> Remove(ItemData itemStack)
    {
        lock (_lock)
        {
            for (var i = 0; i < Contents.Length; i++)
            {
                ItemData slotItemStack = Contents[i];
                if (slotItemStack.ID != itemStack.ID)
                {
                    continue;
                }

                return Remove(i, itemStack.Count);
            }

            return Result<ItemData>.FromFailure("Item not found");
        }
    }

    public Result<ItemData> Remove(int slot, int amount = -1)
    {
        lock (_lock)
        {
            if (slot < 0 || slot >= Contents.Length)
            {
                return Result<ItemData>.FromFailure("Slot is out of bounds");
            }

            ItemData slotItemStack = Contents[slot];
            if (slotItemStack.Count <= 0 || string.IsNullOrEmpty(slotItemStack.ID))
            {
                return Result<ItemData>.FromFailure("Slot is empty");
            }

            if (amount < 0)
            {
                amount = slotItemStack.Count;
            }
            else
            {
                amount = Math.Min(amount, slotItemStack.Count);
            }

            int remaining = slotItemStack.Count - amount;
            slotItemStack.Count = remaining;
            Contents[slot] = remaining > 0 ? slotItemStack : default;

            int amountTaken = remaining >= 0 ? amount : amount + remaining;
            ItemData takenStack = slotItemStack with { Count = amountTaken };

            return Result<ItemData>.FromSuccess(takenStack);
        }
    }

    public bool Swap(int slot1, int slot2)
    {
        lock (_lock)
        {
            if (slot1 < 0 || slot1 >= Contents.Length || slot2 < 0 || slot2 >= Contents.Length)
            {
                return false;
            }

            (Contents[slot1], Contents[slot2]) = (Contents[slot2], Contents[slot1]);
            return true;
        }
    }
}