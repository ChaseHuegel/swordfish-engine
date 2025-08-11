using Swordfish.ECS;
using WaywardBeyond.Client.Core.Items;

namespace WaywardBeyond.Client.Core.Components;

internal struct InventoryComponent(in int size) : IDataComponent
{
    public readonly ItemStack[] Contents = new ItemStack[size];

    public bool Add(ItemStack itemStack)
    {
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
        ItemStack slotItemStack = Contents[slot];
        if (string.IsNullOrEmpty(slotItemStack.ID))
        {
            return false;
        }
        
        slotItemStack.Count += amount;
        Contents[slot] = slotItemStack;
        return true;
    }
    
    public bool Remove(ItemStack itemStack)
    {
        for (var i = 0; i < Contents.Length; i++)
        {
            ItemStack slotItemStack = Contents[i];
            if (slotItemStack.ID != itemStack.ID)
            {
                continue;
            }

            slotItemStack.Count -= itemStack.Count;
            Contents[i] = slotItemStack;
            return true;
        }

        return false;
    }
    
    public bool Remove(int slot, int amount)
    {
        ItemStack slotItemStack = Contents[slot];
        if (slotItemStack.Count <= 0 || string.IsNullOrEmpty(slotItemStack.ID))
        {
            return false;
        }
        
        slotItemStack.Count -= amount;
        Contents[slot] = slotItemStack;
        return true;
    }
}