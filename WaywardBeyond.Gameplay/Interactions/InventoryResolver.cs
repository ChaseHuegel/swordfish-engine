using System;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>Shared slot-move resolution for client prediction and server authority. An invalid op is a no-op, never an error.</summary>
public static class InventoryResolver
{
    /// <summary>Applies a slot move op to an inventory. Returns true when the op changed the inventory.</summary>
    public static bool Apply(ref InventoryComponent inventory, in SlotMoveOp op)
    {
        if (inventory.Contents == null || op.FromSlot < 0 || op.FromSlot >= inventory.Contents.Length)
        {
            return false;
        }

        if (op.Mode == SlotMoveOpMode.AutoStack)
        {
            return ApplyAutoStack(ref inventory, op.FromSlot);
        }

        //  Exact move: destination must be in bounds and distinct from source; a null count means the whole source stack.
        if (op.ToSlot < 0 || op.ToSlot >= inventory.Contents.Length || op.FromSlot == op.ToSlot)
        {
            return false;
        }

        ItemData source = inventory.Contents[op.FromSlot];
        if (source.IsEmpty() || op.Count == 0)
        {
            return false;
        }

        ItemData taken = source;
        if (op.Count != null)
        {
            //  A wire count wider than the stack is the whole stack. Compare before narrowing so a
            //  count above int.MaxValue can never become negative and invert the stack arithmetic.
            uint requested = op.Count.Value;
            taken.Count = requested >= (uint)taken.Count ? taken.Count : (int)requested;
        }

        ItemData destination = inventory.Contents[op.ToSlot];
        bool destinationEmpty = destination.IsEmpty();

        if (destinationEmpty)
        {
            inventory.Contents[op.ToSlot] = taken;
            int newCount = source.Count - taken.Count;
            inventory.Contents[op.FromSlot] = newCount > 0 ? source with { Count = newCount } : default;
            return true;
        }

        if (destination.ID == source.ID)
        {
            //  Same item: stack up to the destination's capacity.
            int available = destination.MaxSize - destination.Count;
            int moved = Math.Min(taken.Count, available);
            if (moved <= 0)
            {
                return false;
            }

            ItemData stacked = destination with { Count = destination.Count + moved };
            inventory.Contents[op.ToSlot] = stacked;
            int newCount = source.Count - moved;
            inventory.Contents[op.FromSlot] = newCount > 0 ? source with { Count = newCount } : default;
            return true;
        }

        //  Different item: a whole-stack move swaps the slots; a partial move is a no-op.
        if (op.Count != null)
        {
            return false;
        }

        inventory.Contents[op.FromSlot] = destination;
        inventory.Contents[op.ToSlot] = source;
        return true;
    }

    /// <summary>
    /// Resolves an AutoStack move: whole source stack to the first same-item stack with capacity, else
    /// the first empty slot, scanning from the opposite region (a hotbar slot moves into the inventory,
    /// an inventory slot moves into the hotbar). No target means a no-op.
    /// </summary>
    private static bool ApplyAutoStack(ref InventoryComponent inventory, int fromSlot)
    {
        ItemData source = inventory.Contents[fromSlot];
        if (source.IsEmpty())
        {
            return false;
        }

        int startSlot = fromSlot < CharacterDefaults.HOTBAR_SLOT_COUNT ? CharacterDefaults.HOTBAR_SLOT_COUNT : 0;
        int firstEmptySlot = -1;

        for (var i = startSlot; i < inventory.Contents.Length; i++)
        {
            if (i == fromSlot)
            {
                continue;
            }

            ItemData slotItem = inventory.Contents[i];
            if (firstEmptySlot == -1 && slotItem.IsEmpty())
            {
                firstEmptySlot = i;
            }

            if (slotItem.ID != source.ID)
            {
                continue;
            }

            int available = slotItem.MaxSize - slotItem.Count;
            if (available <= 0)
            {
                continue;
            }

            int moved = Math.Min(source.Count, available);
            inventory.Contents[i] = slotItem with { Count = slotItem.Count + moved };
            int newCount = source.Count - moved;
            inventory.Contents[fromSlot] = newCount > 0 ? source with { Count = newCount } : default;
            return true;
        }

        if (firstEmptySlot == -1)
        {
            return false;
        }

        inventory.Contents[firstEmptySlot] = source;
        inventory.Contents[fromSlot] = default;
        return true;
    }

    /// <summary>Returns the first empty slot index at or after <paramref name="startingSlot"/>, or -1. A negative start is treated as zero.</summary>
    public static int FindFirstEmptySlot(in InventoryComponent inventory, int startingSlot = 0)
    {
        if (inventory.Contents == null)
        {
            return -1;
        }

        if (startingSlot < 0)
        {
            startingSlot = 0;
        }

        for (var i = startingSlot; i < inventory.Contents.Length; i++)
        {
            ItemData slotItem = inventory.Contents[i];
            if (slotItem.IsEmpty())
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Returns true when <paramref name="toSlot"/> can receive items distributed from <paramref name="fromSlot"/>.</summary>
    public static bool CanDistributeToSlot(in InventoryComponent inventory, int fromSlot, int toSlot)
    {
        if (inventory.Contents == null
            || fromSlot < 0
            || fromSlot >= inventory.Contents.Length
            || toSlot < 0
            || toSlot >= inventory.Contents.Length)
        {
            return false;
        }

        ItemData source = inventory.Contents[fromSlot];
        if (source.IsEmpty())
        {
            return false;
        }

        if (fromSlot == toSlot)
        {
            return true;
        }

        ItemData destination = inventory.Contents[toSlot];
        if (destination.IsEmpty())
        {
            return true;
        }

        return destination.ID == source.ID && destination.Count < destination.MaxSize;
    }

    /// <summary>
    /// Computes the even distribution of <paramref name="fromSlot"/> across <paramref name="targetSlots"/>,
    /// writing each target slot's added count into <paramref name="allocations"/> and returning the remaining count.
    /// </summary>
    public static int ComputeDistribution(
        in InventoryComponent inventory,
        int fromSlot,
        ReadOnlySpan<int> targetSlots,
        Span<int> allocations
    )
    {
        allocations.Clear();

        if (inventory.Contents == null || fromSlot < 0 || fromSlot >= inventory.Contents.Length)
        {
            return 0;
        }

        ItemData source = inventory.Contents[fromSlot];
        if (source.IsEmpty())
        {
            return 0;
        }

        int count = Math.Min(targetSlots.Length, allocations.Length);
        var eligibleCount = 0;

        for (var i = 0; i < count && eligibleCount < source.Count; i++)
        {
            int toSlot = targetSlots[i];
            if (!CanDistributeToSlot(in inventory, fromSlot, toSlot) || ContainsSlot(targetSlots[..i], toSlot))
            {
                continue;
            }

            eligibleCount++;
        }

        if (eligibleCount == 0)
        {
            return source.Count;
        }

        int perSlot = source.Count / eligibleCount;
        var totalAllocated = 0;
        var assignedCount = 0;

        for (var i = 0; i < count && assignedCount < eligibleCount; i++)
        {
            int toSlot = targetSlots[i];
            if (!CanDistributeToSlot(in inventory, fromSlot, toSlot) || ContainsSlot(targetSlots[..i], toSlot))
            {
                continue;
            }

            assignedCount++;
            int available = toSlot == fromSlot
                ? source.MaxSize
                : GetAvailableCapacity(in inventory.Contents[toSlot], in source);

            int share = Math.Min(perSlot, available);
            allocations[i] = share;
            totalAllocated += share;
        }

        return Math.Max(0, source.Count - totalAllocated);
    }

    private static bool ContainsSlot(ReadOnlySpan<int> slots, int slot)
    {
        for (var i = 0; i < slots.Length; i++)
        {
            if (slots[i] == slot)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetAvailableCapacity(in ItemData destination, in ItemData source)
    {
        if (destination.IsEmpty())
        {
            return source.MaxSize;
        }

        return Math.Max(0, destination.MaxSize - destination.Count);
    }
}