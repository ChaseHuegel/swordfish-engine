using System;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>Shared keyed slot-transfer resolution for client prediction and server authority.</summary>
public static class InventoryResolver
{
    /// <summary>Applies an intra-inventory transfer op using default validation.</summary>
    public static bool Apply(ref InventoryComponent inventory, in TransferOp op)
    {
        return Apply(ref inventory, in op, InventoryValidationContext.Default);
    }

    /// <summary>Applies an intra-inventory transfer op with schema and stack validation.</summary>
    public static bool Apply(ref InventoryComponent inventory, in TransferOp op, in InventoryValidationContext context)
    {
        if (op.FromContainer != op.ToContainer)
        {
            return false;
        }

        inventory.Hydrate();
        SlotGroupLayout layout = inventory.Layout;
        if (!op.FromSlot.IsValid || !layout.TryResolveIndex(in op.FromSlot, out _))
        {
            return false;
        }

        if (op.Mode == TransferMode.AutoStack)
        {
            return ApplyAutoStackIntra(ref inventory, in op.FromSlot, in op.ToSlot, in context);
        }

        if (op.Mode != TransferMode.Exact)
        {
            return false;
        }

        if (!op.ToSlot.IsValid || !layout.TryResolveIndex(in op.ToSlot, out _) || op.FromSlot == op.ToSlot)
        {
            return false;
        }

        return ApplyExactCore(ref inventory, in op.FromSlot, ref inventory, in op.ToSlot, op.Count, in context);
    }

    /// <summary>Applies a cross-container transfer op with schema and stack validation.</summary>
    public static bool Apply(
        ref InventoryComponent source,
        ref InventoryComponent destination,
        in TransferOp op,
        in InventoryValidationContext context
    )
    {
        source.Hydrate();
        destination.Hydrate();

        if (!op.FromSlot.IsValid || !source.Layout.TryResolveIndex(in op.FromSlot, out _))
        {
            return false;
        }

        if (op.Mode == TransferMode.AutoStack)
        {
            return ApplyAutoStackCross(ref source, in op.FromSlot, ref destination, in op.ToSlot, in context);
        }

        if (op.Mode != TransferMode.Exact)
        {
            return false;
        }

        if (!op.ToSlot.IsValid || !destination.Layout.TryResolveIndex(in op.ToSlot, out _))
        {
            return false;
        }

        return ApplyExactCore(ref source, in op.FromSlot, ref destination, in op.ToSlot, op.Count, in context);
    }

    /// <summary>Finds the first empty general-storage slot in <paramref name="inventory"/>.</summary>
    public static Result<SlotId> FindFirstEmptySlot(in InventoryComponent inventory)
    {
        return FindFirstEmptySlot(in inventory, default, InventoryValidationContext.Default);
    }

    /// <summary>Finds the first empty slot at or after <paramref name="startingSlot"/>.</summary>
    public static Result<SlotId> FindFirstEmptySlot(in InventoryComponent inventory, in SlotId startingSlot)
    {
        return FindFirstEmptySlot(in inventory, in startingSlot, InventoryValidationContext.Default);
    }

    /// <summary>Finds the first empty permitted slot at or after <paramref name="startingSlot"/>.</summary>
    public static Result<SlotId> FindFirstEmptySlot(
        in InventoryComponent inventory,
        in SlotId startingSlot,
        in InventoryValidationContext context
    )
    {
        SlotGroupLayout layout = inventory.Layout;
        if (layout.TotalCapacity <= 0)
        {
            return Result<SlotId>.FromFailure("Inventory has no slots.");
        }

        ReadOnlySpan<SlotGroupRange> groups = layout.GroupSpan;
        var startGroupIndex = 0;
        var startLocalIndex = 0;
        bool explicitGroup = !string.IsNullOrEmpty(startingSlot.GroupId);

        if (explicitGroup)
        {
            var foundGroup = false;
            for (var g = 0; g < groups.Length; g++)
            {
                if (string.Equals(groups[g].GroupId, startingSlot.GroupId, StringComparison.Ordinal))
                {
                    startGroupIndex = g;
                    startLocalIndex = Math.Max(0, startingSlot.LocalIndex);
                    foundGroup = true;
                    break;
                }
            }

            if (!foundGroup)
            {
                return Result<SlotId>.FromFailure($"Unknown slot group \"{startingSlot.GroupId}\".");
            }
        }

        for (var g = startGroupIndex; g < groups.Length; g++)
        {
            SlotGroupRange range = groups[g];
            if ((!explicitGroup || g != startGroupIndex) && IsFilteredStorageGroup(range.GroupId, in context))
            {
                continue;
            }

            int localStart = g == startGroupIndex ? startLocalIndex : 0;
            for (int local = localStart; local < range.Capacity; local++)
            {
                var candidate = new SlotId(range.GroupId, local);
                if (inventory.Get(in candidate).IsEmpty())
                {
                    return Result<SlotId>.FromSuccess(candidate);
                }
            }
        }

        return Result<SlotId>.FromFailure("No empty slot found.");
    }

    /// <summary>Returns true when <paramref name="toSlot"/> can receive items distributed from <paramref name="fromSlot"/>.</summary>
    public static bool CanDistributeToSlot(in InventoryComponent inventory, in SlotId fromSlot, in SlotId toSlot)
    {
        return CanDistributeToSlot(in inventory, in fromSlot, in toSlot, InventoryValidationContext.Default);
    }

    /// <summary>Returns true when <paramref name="toSlot"/> can receive items distributed from <paramref name="fromSlot"/>.</summary>
    public static bool CanDistributeToSlot(
        in InventoryComponent inventory,
        in SlotId fromSlot,
        in SlotId toSlot,
        in InventoryValidationContext context
    )
    {
        SlotGroupLayout layout = inventory.Layout;
        if (!layout.TryResolveIndex(in fromSlot, out _) || !layout.TryResolveIndex(in toSlot, out _))
        {
            return false;
        }

        ItemData source = inventory.Get(in fromSlot);
        if (source.IsEmpty() || !context.AllowsItem(in toSlot, in source))
        {
            return false;
        }

        if (fromSlot == toSlot)
        {
            return true;
        }

        ItemData destination = inventory.Get(in toSlot);
        if (destination.IsEmpty())
        {
            return true;
        }

        int effectiveMax = context.ResolveMergeMaxStack(in destination, in source);
        return string.Equals(destination.ID, source.ID, StringComparison.Ordinal) && destination.Count < effectiveMax;
    }

    /// <summary>Computes even distribution of <paramref name="fromSlot"/> across <paramref name="targetSlots"/>.</summary>
    public static int ComputeDistribution(
        in InventoryComponent inventory,
        in SlotId fromSlot,
        ReadOnlySpan<SlotId> targetSlots,
        Span<int> allocations
    )
    {
        return ComputeDistribution(in inventory, in fromSlot, targetSlots, allocations, InventoryValidationContext.Default);
    }

    /// <summary>Computes even distribution of <paramref name="fromSlot"/> across <paramref name="targetSlots"/>.</summary>
    public static int ComputeDistribution(
        in InventoryComponent inventory,
        in SlotId fromSlot,
        ReadOnlySpan<SlotId> targetSlots,
        Span<int> allocations,
        in InventoryValidationContext context
    )
    {
        allocations.Clear();

        if (!inventory.Layout.TryResolveIndex(in fromSlot, out _))
        {
            return 0;
        }

        ItemData source = inventory.Get(in fromSlot);
        if (source.IsEmpty())
        {
            return 0;
        }

        int count = Math.Min(targetSlots.Length, allocations.Length);
        var eligibleCount = 0;

        for (var i = 0; i < count && eligibleCount < source.Count; i++)
        {
            SlotId toSlot = targetSlots[i];
            if (!CanDistributeToSlot(in inventory, in fromSlot, in toSlot, in context) || ContainsSlot(targetSlots[..i], in toSlot))
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
            SlotId toSlot = targetSlots[i];
            if (!CanDistributeToSlot(in inventory, in fromSlot, in toSlot, in context) || ContainsSlot(targetSlots[..i], in toSlot))
            {
                continue;
            }

            assignedCount++;
            int available = toSlot == fromSlot
                ? context.ResolveMaxStack(in source)
                : GetAvailableCapacity(inventory.Get(in toSlot), in source, in context);

            int share = Math.Min(perSlot, available);
            allocations[i] = share;
            totalAllocated += share;
        }

        return Math.Max(0, source.Count - totalAllocated);
    }

    private static bool ApplyExactCore(
        ref InventoryComponent sourceInventory,
        in SlotId fromSlot,
        ref InventoryComponent destinationInventory,
        in SlotId toSlot,
        uint? count,
        in InventoryValidationContext context
    )
    {
        ItemData source = sourceInventory.Get(in fromSlot);
        if (source.IsEmpty() || count == 0)
        {
            return false;
        }

        if (!context.AllowsItem(in toSlot, in source))
        {
            return false;
        }

        int takenCount = source.Count;
        if (count != null)
        {
            //  Compare before narrowing so counts above int.MaxValue cannot wrap negative.
            uint requested = count.Value;
            takenCount = requested >= (uint)takenCount ? takenCount : (int)requested;
        }

        ItemData destination = destinationInventory.Get(in toSlot);
        if (destination.IsEmpty())
        {
            int effectiveMax = context.ResolveMaxStack(in source);
            int moved = Math.Min(takenCount, effectiveMax);
            if (moved <= 0)
            {
                return false;
            }

            int remaining = source.Count - moved;
            ItemData placed = source with { Count = moved, MaxSize = effectiveMax };
            ItemData leftBehind = remaining > 0 ? source with { Count = remaining } : default;
            destinationInventory.Set(in toSlot, in placed);
            sourceInventory.Set(in fromSlot, in leftBehind);
            return true;
        }

        if (string.Equals(destination.ID, source.ID, StringComparison.Ordinal))
        {
            int effectiveMax = context.ResolveMergeMaxStack(in destination, in source);
            int available = effectiveMax - destination.Count;
            int moved = Math.Min(takenCount, available);
            if (moved <= 0)
            {
                return false;
            }

            ItemData stacked = destination with { Count = destination.Count + moved, MaxSize = effectiveMax };
            int remaining = source.Count - moved;
            ItemData leftBehind = remaining > 0 ? source with { Count = remaining } : default;
            destinationInventory.Set(in toSlot, in stacked);
            sourceInventory.Set(in fromSlot, in leftBehind);
            return true;
        }

        //  Swapping different items requires a whole-stack move and mutual filter/stack compatibility.
        if (count != null || !context.AllowsItem(in fromSlot, in destination))
        {
            return false;
        }

        int destMaxForSource = context.ResolveMaxStack(in source);
        int sourceMaxForDest = context.ResolveMaxStack(in destination);
        if (source.Count > destMaxForSource || destination.Count > sourceMaxForDest)
        {
            return false;
        }

        ItemData newFrom = destination with { MaxSize = sourceMaxForDest };
        ItemData newTo = source with { MaxSize = destMaxForSource };
        destinationInventory.Set(in toSlot, in newTo);
        sourceInventory.Set(in fromSlot, in newFrom);
        return true;
    }

    private static bool ApplyAutoStackIntra(
        ref InventoryComponent inventory,
        in SlotId fromSlot,
        in SlotId requestedTarget,
        in InventoryValidationContext context
    )
    {
        if (!string.IsNullOrEmpty(requestedTarget.GroupId))
        {
            return ApplyAutoStackNamedGroups(ref inventory, in fromSlot, ref inventory, requestedTarget.GroupId, null, fromSlot, in context);
        }

        if (string.Equals(fromSlot.GroupId, SlotGroups.HOTBAR, StringComparison.Ordinal))
        {
            return ApplyAutoStackNamedGroups(ref inventory, in fromSlot, ref inventory, SlotGroups.BACKPACK, null, fromSlot, in context);
        }

        if (string.Equals(fromSlot.GroupId, SlotGroups.BACKPACK, StringComparison.Ordinal))
        {
            return ApplyAutoStackNamedGroups(ref inventory, in fromSlot, ref inventory, SlotGroups.HOTBAR, null, fromSlot, in context);
        }

        return ApplyAutoStackNamedGroups(ref inventory, in fromSlot, ref inventory, SlotGroups.BACKPACK, SlotGroups.HOTBAR, fromSlot, in context);
    }

    private static bool ApplyAutoStackCross(
        ref InventoryComponent sourceInventory,
        in SlotId fromSlot,
        ref InventoryComponent destinationInventory,
        in SlotId requestedTarget,
        in InventoryValidationContext context
    )
    {
        if (!string.IsNullOrEmpty(requestedTarget.GroupId))
        {
            return ApplyAutoStackNamedGroups(ref sourceInventory, in fromSlot, ref destinationInventory, requestedTarget.GroupId, null, skipSlot: null, in context);
        }

        return ApplyAutoStackRanges(ref sourceInventory, in fromSlot, ref destinationInventory, destinationInventory.Layout.GroupSpan, skipSlot: null, in context);
    }

    private static bool ApplyAutoStackNamedGroups(
        ref InventoryComponent sourceInventory,
        in SlotId fromSlot,
        ref InventoryComponent destinationInventory,
        string firstGroupId,
        string? secondGroupId,
        SlotId? skipSlot,
        in InventoryValidationContext context
    )
    {
        SlotGroupLayout destLayout = destinationInventory.Layout;
        var count = 0;
        SlotGroupRange first = default;
        SlotGroupRange second = default;

        if (destLayout.TryGetGroup(firstGroupId, out SlotGroupRange firstRange))
        {
            first = firstRange;
            count = 1;
        }

        if (secondGroupId != null && destLayout.TryGetGroup(secondGroupId, out SlotGroupRange secondRange))
        {
            if (count == 0)
            {
                first = secondRange;
            }
            else
            {
                second = secondRange;
            }

            count++;
        }

        if (count == 0)
        {
            return false;
        }

        ReadOnlySpan<SlotGroupRange> ranges = count == 1 ? [first] : [first, second];
        return ApplyAutoStackRanges(ref sourceInventory, in fromSlot, ref destinationInventory, ranges, skipSlot, in context);
    }

    private static bool ApplyAutoStackRanges(
        ref InventoryComponent sourceInventory,
        in SlotId fromSlot,
        ref InventoryComponent destinationInventory,
        ReadOnlySpan<SlotGroupRange> targetGroups,
        SlotId? skipSlot,
        in InventoryValidationContext context
    )
    {
        ItemData source = sourceInventory.Get(in fromSlot);
        if (source.IsEmpty() || targetGroups.Length == 0)
        {
            return false;
        }

        var state = new AutoStackState(source.Count, context.ResolveMaxStack(in source), skipSlot);

        for (var g = 0; g < targetGroups.Length && state.Remaining > 0; g++)
        {
            StackAutoRange(ref destinationInventory, in targetGroups[g], in source, in context, ref state);
        }

        if (state.Remaining > 0 && state.HasEmpty)
        {
            int moved = Math.Min(state.Remaining, state.EffectiveMax);
            if (moved > 0)
            {
                destinationInventory.Set(in state.FirstEmpty, source with { Count = moved, MaxSize = state.EffectiveMax });
                state.Remaining -= moved;
                state.Changed = true;
            }
        }

        if (!state.Changed)
        {
            return false;
        }

        ItemData leftBehind = state.Remaining > 0 ? source with { Count = state.Remaining } : default;
        sourceInventory.Set(in fromSlot, in leftBehind);
        return true;
    }

    private static void StackAutoRange(
        ref InventoryComponent destinationInventory,
        in SlotGroupRange range,
        in ItemData source,
        in InventoryValidationContext context,
        ref AutoStackState state
    )
    {
        for (var i = 0; i < range.Capacity && state.Remaining > 0; i++)
        {
            var candidate = new SlotId(range.GroupId, i);
            if (state.SkipSlot.HasValue && candidate == state.SkipSlot.Value)
            {
                continue;
            }

            if (!context.AllowsItem(in candidate, in source))
            {
                continue;
            }

            ItemData slotItem = destinationInventory.Get(in candidate);
            if (slotItem.IsEmpty())
            {
                if (!state.HasEmpty)
                {
                    state.FirstEmpty = candidate;
                    state.HasEmpty = true;
                }

                continue;
            }

            if (!string.Equals(slotItem.ID, source.ID, StringComparison.Ordinal))
            {
                continue;
            }

            int stackMax = Math.Min(Math.Max(1, slotItem.MaxSize), state.EffectiveMax);
            int available = stackMax - slotItem.Count;
            if (available <= 0)
            {
                continue;
            }

            int moved = Math.Min(state.Remaining, available);
            destinationInventory.Set(in candidate, slotItem with { Count = slotItem.Count + moved, MaxSize = stackMax });
            state.Remaining -= moved;
            state.Changed = true;
        }
    }

    private static bool IsFilteredStorageGroup(string groupId, in InventoryValidationContext context)
    {
        Result<SlotGroupSchema> schema = context.Schemas.Get(groupId);
        return schema.Success && (schema.Value.AllowedTags.Count > 0 || schema.Value.SlotAllowedTags.Count > 0);
    }

    private static bool ContainsSlot(ReadOnlySpan<SlotId> slots, in SlotId slot)
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

    private static int GetAvailableCapacity(in ItemData destination, in ItemData source, in InventoryValidationContext context)
    {
        if (destination.IsEmpty())
        {
            return context.ResolveMaxStack(in source);
        }

        int destMax = context.ResolveMergeMaxStack(in destination, in source);
        return Math.Max(0, destMax - destination.Count);
    }

    private struct AutoStackState(int remaining, int effectiveMax, SlotId? skipSlot)
    {
        public int Remaining = remaining;
        public readonly int EffectiveMax = effectiveMax;
        public readonly SlotId? SkipSlot = skipSlot;
        public SlotId FirstEmpty;
        public bool HasEmpty;
        public bool Changed;
    }
}
