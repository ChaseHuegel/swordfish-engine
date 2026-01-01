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
        
        int firstEmptySlot = -1;
        for (var i = 0; i < Contents.Length; i++)
        {
            ItemStack slotItemStack = Contents[i];

            if (firstEmptySlot == -1 && (slotItemStack.Count == 0 || string.IsNullOrEmpty(slotItemStack.ID)))
            {
                firstEmptySlot = i;
            }

            if (slotItemStack.ID != itemStack.ID)
            {
                continue;
            }

            slotItemStack.Count += itemStack.Count;
            Contents[i] = slotItemStack;
            return true;
        }

        if (firstEmptySlot == -1)
        {
            return false;
        }

        Contents[firstEmptySlot] = itemStack;
        return true;
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
        Contents[slot] = slotItemStack;
        
        int amountTaken = remaining >= 0 ? amount : amount + remaining;
        ItemStack takenStack = slotItemStack with { Count = amountTaken };
        
        return Result<ItemStack>.FromSuccess(takenStack);
    }
    
    public bool Swap(int slot1, int slot2)
    {
        using Lock.Scope _ = _lock.EnterScope();
        
        Result<ItemStack> item1 = Remove(slot1);
        Result<ItemStack> item2 = Remove(slot2);
        Contents[slot1] = item2;
        Contents[slot2] = item1;
        
        return true;
    }
}