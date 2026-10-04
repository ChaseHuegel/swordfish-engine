using System;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// The shared, single-location inventory move resolver. Client prediction and server authority both call
/// it to apply a client-sent <see cref="SlotMoveOp"/> to an <see cref="InventoryComponent"/>, so the
/// predicted result equals the authoritative result for a valid op. The server remains authoritative: it
/// validates bounds against its own copy and its <see cref="InventoryComponent"/> echo corrects any
/// client state that drifted. An invalid op (out-of-bounds slot, empty source, partial move onto a
/// different item) resolves to a no-op and never throws.
/// </summary>
public static class SharedInventoryResolver
{
    /// <summary>
    /// Applies a slot move op to an inventory, mirroring the server's deterministic resolution. Returns
    /// true when the op changed the inventory.
    /// </summary>
    public static bool Apply(ref InventoryComponent inventory, in SlotMoveOp op)
    {
        if (inventory.Contents == null || op.FromSlot < 0 || op.FromSlot >= inventory.Contents.Length)
        {
            return false;
        }

        if (op.Mode == SlotMoveOp.MODE_AUTO_STACK)
        {
            return ApplyAutoStack(ref inventory, op.FromSlot);
        }

        //  Exact move: destination must be in bounds; a null count means the whole source stack.
        if (op.ToSlot < 0 || op.ToSlot >= inventory.Contents.Length)
        {
            return false;
        }

        ItemData source = inventory.Contents[op.FromSlot];
        if (source.Count <= 0 || string.IsNullOrEmpty(source.ID) || op.Count == 0)
        {
            return false;
        }

        ItemData taken = source;
        if (op.Count != null)
        {
            taken.Count = Math.Min(taken.Count, (int)op.Count.Value);
        }

        ItemData destination = inventory.Contents[op.ToSlot];
        bool destinationEmpty = destination.Count <= 0 || string.IsNullOrEmpty(destination.ID);

        if (destinationEmpty)
        {
            inventory.Contents[op.ToSlot] = taken;
            inventory.Contents[op.FromSlot] = source with { Count = source.Count - taken.Count };
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
            inventory.Contents[op.FromSlot] = source with { Count = source.Count - moved };
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
        if (source.Count <= 0 || string.IsNullOrEmpty(source.ID))
        {
            return false;
        }

        int startSlot = fromSlot < 9 ? 9 : 0;
        int firstEmptySlot = -1;

        for (var i = startSlot; i < inventory.Contents.Length; i++)
        {
            if (i == fromSlot)
            {
                continue;
            }

            ItemData slotItem = inventory.Contents[i];
            if (firstEmptySlot == -1 && (slotItem.Count <= 0 || string.IsNullOrEmpty(slotItem.ID)))
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
            inventory.Contents[fromSlot] = source with { Count = source.Count - moved };
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

    /// <summary>Returns the first empty slot index at or after <paramref name="startingSlot"/>, or -1.</summary>
    public static int FindFirstEmptySlot(in InventoryComponent inventory, int startingSlot = 0)
    {
        if (inventory.Contents == null)
        {
            return -1;
        }

        for (var i = startingSlot; i < inventory.Contents.Length; i++)
        {
            ItemData slotItem = inventory.Contents[i];
            if (slotItem.Count <= 0 || string.IsNullOrEmpty(slotItem.ID))
            {
                return i;
            }
        }

        return -1;
    }
}