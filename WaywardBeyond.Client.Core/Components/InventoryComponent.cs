using System;
using System.Threading;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Items;

namespace WaywardBeyond.Client.Core.Components;

internal struct InventoryComponent(in int size) : IDataComponent
{
    public readonly ItemStack[] Contents = new ItemStack[size];

    private readonly Lock _lock = new();

    public bool Add(ItemStack itemStack)
    {
        using Lock.Scope _ = _lock.EnterScope();

        for (var startSlot = 0; startSlot < Contents.Length; startSlot++)
        {
            int firstEmptySlot = -1;
            for (int i = startSlot; i < Contents.Length; i++)
            {
                if (itemStack.Count <= 0)
                {
                    //  The stack has been consumed, or was empty to begin with.
                    return true;
                }

                ItemStack slotItemStack = Contents[i];

                if (firstEmptySlot == -1 && (slotItemStack.Count == 0 || string.IsNullOrEmpty(slotItemStack.ID)))
                {
                    firstEmptySlot = i;
                }

                if (slotItemStack.ID != itemStack.ID)
                {
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
                //  There is no slot to place the stack into.
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
    
    public bool Add(int slot, int amount)
    {
        using Lock.Scope _ = _lock.EnterScope();
        
        ItemStack slotItemStack = Contents[slot];
        if (string.IsNullOrEmpty(slotItemStack.ID))
        {
            return false;
        }
        
        slotItemStack.Count += amount;
        Contents[slot] = slotItemStack;
        return true;
    }
    
    public Result<ItemStack> Remove(ItemStack itemStack)
    {
        using Lock.Scope _ = _lock.EnterScope();
        
        for (var i = 0; i < Contents.Length; i++)
        {
            ItemStack slotItemStack = Contents[i];
            if (slotItemStack.ID != itemStack.ID)
            {
                continue;
            }
            
            return Remove(i, itemStack.Count);
        }
        
        return Result<ItemStack>.FromFailure("Item not found");
    }
    
    public Result<ItemStack> Remove(int slot, int amount = -1)
    {
        using Lock.Scope _ = _lock.EnterScope();
        
        if (slot < 0 || slot >= Contents.Length)
        {
            return Result<ItemStack>.FromFailure("Slot is out of bounds");
        }
        
        ItemStack slotItemStack = Contents[slot];
        if (slotItemStack.Count <= 0 || string.IsNullOrEmpty(slotItemStack.ID))
        {
            return Result<ItemStack>.FromFailure("Slot is empty");
        }
        
        if (amount < 0)
        {
            amount = slotItemStack.Count;
        }
        
        int remaining = slotItemStack.Count - amount;
        
        slotItemStack.Count = remaining;
        Contents[slot] = remaining > 0 ? slotItemStack : ItemStack.Empty;
        
        int amountTaken = remaining >= 0 ? amount : amount + remaining;
        ItemStack takenStack = slotItemStack with { Count = amountTaken };
        
        return Result<ItemStack>.FromSuccess(takenStack);
    }
    
    public bool Swap(int slot1, int slot2)
    {
        using Lock.Scope _ = _lock.EnterScope();

        if (slot1 < 0 || slot1 >= Contents.Length || slot2 < 0 || slot2 >= Contents.Length)
        {
            return false;
        }
        
        Result<ItemStack> item1 = Remove(slot1);
        Result<ItemStack> item2 = Remove(slot2);
        Contents[slot1] = item2;
        Contents[slot2] = item1;
        
        return true;
    }
}