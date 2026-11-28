using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Krill;
using Swordfish.Library.Util;

namespace WaywardBeyond.Data.Components;

/// <summary>The server-authoritative keyed inventory component backed by a contiguous slot store.</summary>
[StructLayout(LayoutKind.Auto)]
public partial struct InventoryComponent : IDataComponent
{
    private ItemData[]? _items;
    private SlotGroupLayout? _layout;
    private List<KeyedSlotEntry>? _orphans;

    /// <summary>Constructs an inventory backed by <paramref name="layout"/>.</summary>
    public InventoryComponent(SlotGroupLayout layout)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _items = layout.TotalCapacity > 0 ? new ItemData[layout.TotalCapacity] : [];
        _orphans = null;
        Groups = layout.ToDescriptors();
        Slots = [];
    }

    /// <summary>Creates a canonical player inventory using capacities from <paramref name="schemas"/>.</summary>
    public static InventoryComponent CreatePlayer(ISlotGroupSchemaDatabase? schemas = null)
    {
        return new InventoryComponent(SlotGroupLayout.CreatePlayer(schemas));
    }

    /// <summary>Creates a container inventory with the specified group descriptors.</summary>
    public static InventoryComponent Create(params ReadOnlySpan<SlotGroupDescriptor> groups)
    {
        return new InventoryComponent(SlotGroupLayout.Create(groups));
    }

    /// <summary>The group-offset layout for this inventory.</summary>
    public readonly SlotGroupLayout Layout => _layout ?? SlotGroupLayout.FromDescriptors(Groups);

    /// <summary>Total number of addressable slots across all groups.</summary>
    public readonly int TotalCapacity
    {
        get
        {
            if (_layout != null)
            {
                return _layout.TotalCapacity;
            }

            if (Groups == null)
            {
                return 0;
            }

            var total = 0;
            for (var i = 0; i < Groups.Length; i++)
            {
                int capacity = Groups[i].Capacity;
                if (capacity > 0)
                {
                    total = capacity > SlotGroupLayout.MAX_TOTAL_CAPACITY - total
                        ? SlotGroupLayout.MAX_TOTAL_CAPACITY
                        : total + capacity;
                }
            }

            return total;
        }
    }

    /// <summary>Orphaned entries preserved non-destructively when no valid slot or backpack space could receive them.</summary>
    public readonly IReadOnlyList<KeyedSlotEntry> Orphans => _orphans != null ? _orphans.AsReadOnly() : [];

    /// <summary>Clamps a hotbar slot index into <c>[0, capacity - 1]</c>.</summary>
    public static int ClampHotbarSlot(int slot, int capacity = SlotGroups.DEFAULT_HOTBAR_CAPACITY)
    {
        if (capacity <= 0)
        {
            return 0;
        }

        return Math.Clamp(slot, 0, capacity - 1);
    }

    /// <summary>Clamps a hotbar slot index against this inventory's <see cref="SlotGroups.HOTBAR"/> capacity.</summary>
    public readonly int ClampHotbarSlot(int slot)
    {
        int capacity = GetGroupCapacity(SlotGroups.HOTBAR);
        return ClampHotbarSlot(slot, capacity > 0 ? capacity : SlotGroups.DEFAULT_HOTBAR_CAPACITY);
    }

    /// <summary>Builds the contiguous runtime store and recovers any orphaned entries from wire fields.</summary>
    public void Hydrate()
    {
        if (_layout != null && _items != null)
        {
            return;
        }

        _layout = SlotGroupLayout.FromDescriptors(Groups);
        _items = _layout.TotalCapacity > 0 ? new ItemData[_layout.TotalCapacity] : [];
        Groups = _layout.ToDescriptors();
        LoadSparseCore(Slots);
    }

    /// <summary>Returns an independent deep copy of this inventory component.</summary>
    public readonly InventoryComponent Clone()
    {
        if (_layout == null || _items == null)
        {
            var copy = new InventoryComponent
            {
                Groups = Groups != null ? [.. Groups] : [],
                Slots = Slots != null ? [.. Slots] : [],
            };
            copy.Hydrate();
            return copy;
        }

        var clonedItems = _items.Length > 0 ? new ItemData[_items.Length] : [];
        if (_items.Length > 0)
        {
            Array.Copy(_items, clonedItems, _items.Length);
        }

        return new InventoryComponent
        {
            _layout = _layout,
            _items = clonedItems,
            _orphans = _orphans != null && _orphans.Count > 0 ? [.. _orphans] : null,
            Groups = Groups != null ? [.. Groups] : _layout.ToDescriptors(),
            Slots = Slots != null ? [.. Slots] : [],
        };
    }

    /// <summary>Returns whether <paramref name="groupId"/> exists in this inventory.</summary>
    public readonly bool HasGroup(string? groupId)
    {
        if (string.IsNullOrEmpty(groupId))
        {
            return false;
        }

        if (_layout != null)
        {
            return _layout.TryGetGroup(groupId, out _);
        }

        if (Groups == null)
        {
            return false;
        }

        for (var i = 0; i < Groups.Length; i++)
        {
            if (string.Equals(Groups[i].GroupId, groupId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the slot capacity of <paramref name="groupId"/>, or zero when absent.</summary>
    public readonly int GetGroupCapacity(string? groupId)
    {
        if (string.IsNullOrEmpty(groupId))
        {
            return 0;
        }

        if (_layout != null)
        {
            return _layout.GetGroupCapacity(groupId);
        }

        if (Groups == null)
        {
            return 0;
        }

        for (var i = 0; i < Groups.Length; i++)
        {
            if (string.Equals(Groups[i].GroupId, groupId, StringComparison.Ordinal))
            {
                return Math.Max(0, Groups[i].Capacity);
            }
        }

        return 0;
    }

    /// <summary>Reads the item stack stored at <paramref name="slot"/>, or default when empty or out of bounds.</summary>
    public readonly ItemData Get(in SlotId slot)
    {
        if (_layout != null && _items != null)
        {
            return _layout.TryResolveIndex(in slot, out int index) ? _items[index] : default;
        }

        if (Slots == null || !slot.IsValid)
        {
            return default;
        }

        int capacity = GetGroupCapacity(slot.GroupId);
        if (slot.LocalIndex >= capacity)
        {
            return default;
        }

        for (var i = 0; i < Slots.Length; i++)
        {
            KeyedSlotEntry entry = Slots[i];
            if (entry.Item.IsEmpty())
            {
                continue;
            }

            if (entry.LocalIndex == slot.LocalIndex
                && string.Equals(entry.GroupId, slot.GroupId, StringComparison.Ordinal))
            {
                return entry.Item;
            }
        }

        return default;
    }

    /// <summary>Writes <paramref name="item"/> into <paramref name="slot"/>, returning false when the slot does not exist.</summary>
    public bool Set(in SlotId slot, in ItemData item)
    {
        Hydrate();
        if (!_layout!.TryResolveIndex(in slot, out int index))
        {
            return false;
        }

        _items![index] = item.IsEmpty()
            ? default
            : item with { MaxSize = Math.Max(1, item.MaxSize) };
        SyncWireState(includeGroups: false);
        return true;
    }

    /// <summary>Loads sparse keyed entries into the layout, routing unknown or out-of-bounds items to recovery.</summary>
    public void LoadSparse(ReadOnlySpan<KeyedSlotEntry> entries)
    {
        Hydrate();
        Array.Clear(_items!);
        _orphans = null;
        LoadSparseCore(entries);
    }

    /// <summary>Resizes or appends <paramref name="groupId"/> without shifting any other group's logical addresses.</summary>
    public bool ResizeGroup(string groupId, int newCapacity)
    {
        if (string.IsNullOrEmpty(groupId))
        {
            return false;
        }

        Hydrate();
        int clamped = Math.Max(0, newCapacity);
        if (_layout!.TryGetGroup(groupId, out SlotGroupRange existing) && existing.Capacity == clamped)
        {
            return false;
        }

        SlotGroupLayout nextLayout = _layout.WithGroupCapacity(groupId, clamped);
        var nextItems = nextLayout.TotalCapacity > 0 ? new ItemData[nextLayout.TotalCapacity] : [];
        List<KeyedSlotEntry>? truncated = null;

        for (var g = 0; g < _layout.Groups.Count; g++)
        {
            SlotGroupRange oldRange = _layout.Groups[g];
            if (!nextLayout.TryGetGroup(oldRange.GroupId, out SlotGroupRange newRange))
            {
                continue;
            }

            int preserved = Math.Min(oldRange.Capacity, newRange.Capacity);
            for (var i = 0; i < preserved; i++)
            {
                nextItems[newRange.Offset + i] = _items![oldRange.Offset + i];
            }

            for (int i = preserved; i < oldRange.Capacity; i++)
            {
                ItemData dropped = _items![oldRange.Offset + i];
                if (!dropped.IsEmpty())
                {
                    truncated ??= [];
                    truncated.Add(new KeyedSlotEntry(oldRange.GroupId, i, dropped));
                }
            }
        }

        _layout = nextLayout;
        _items = nextItems;

        if (truncated != null)
        {
            AppendOrphans(truncated);
        }

        SyncWireState(includeGroups: true);
        return true;
    }

    /// <summary>Stacks <paramref name="itemStack"/> into general storage groups, returning false if overflow remains.</summary>
    public bool Add(in ItemData itemStack, string? targetGroupId = null, bool onlyEmptySlots = false)
    {
        if (itemStack.IsEmpty())
        {
            return false;
        }

        Hydrate();
        ItemData remaining = itemStack with { MaxSize = Math.Max(1, itemStack.MaxSize) };

        if (targetGroupId != null)
        {
            bool changed = AddToGroup(targetGroupId, ref remaining, onlyEmptySlots);
            if (changed)
            {
                SyncWireState(includeGroups: false);
            }

            return remaining.Count <= 0;
        }

        var anyChanged = false;
        bool hasHotbar = _layout!.TryGetGroup(SlotGroups.HOTBAR, out SlotGroupRange hotbar);
        bool hasBackpack = _layout.TryGetGroup(SlotGroups.BACKPACK, out SlotGroupRange backpack);

        if (hasHotbar || hasBackpack)
        {
            if (!onlyEmptySlots)
            {
                if (hasHotbar)
                {
                    anyChanged |= StackIntoRange(hotbar, ref remaining);
                }

                if (hasBackpack && remaining.Count > 0)
                {
                    anyChanged |= StackIntoRange(backpack, ref remaining);
                }
            }

            if (hasHotbar && remaining.Count > 0)
            {
                anyChanged |= PlaceIntoEmptyRange(hotbar, ref remaining);
            }

            if (hasBackpack && remaining.Count > 0)
            {
                anyChanged |= PlaceIntoEmptyRange(backpack, ref remaining);
            }
        }
        else
        {
            for (var i = 0; i < _layout.Groups.Count && remaining.Count > 0; i++)
            {
                SlotGroupRange range = _layout.Groups[i];
                if (!onlyEmptySlots)
                {
                    anyChanged |= StackIntoRange(range, ref remaining);
                }
            }

            for (var i = 0; i < _layout.Groups.Count && remaining.Count > 0; i++)
            {
                anyChanged |= PlaceIntoEmptyRange(_layout.Groups[i], ref remaining);
            }
        }

        if (anyChanged)
        {
            SyncWireState(includeGroups: false);
        }

        return remaining.Count <= 0;
    }

    /// <summary>Removes up to <paramref name="amount"/> items (or the whole stack when negative) from <paramref name="slot"/>.</summary>
    public Result<ItemData> Remove(in SlotId slot, int amount = -1)
    {
        Hydrate();
        if (!_layout!.TryResolveIndex(in slot, out int index))
        {
            return Result<ItemData>.FromFailure("Slot is out of bounds.");
        }

        ItemData slotStack = _items![index];
        if (slotStack.IsEmpty())
        {
            return Result<ItemData>.FromFailure("Slot is empty.");
        }

        if (amount == 0)
        {
            return Result<ItemData>.FromFailure("Removal amount must be non-zero.");
        }

        int taken = amount < 0 ? slotStack.Count : Math.Min(amount, slotStack.Count);
        int remaining = slotStack.Count - taken;
        _items[index] = remaining > 0 ? slotStack with { Count = remaining } : default;
        SyncWireState(includeGroups: false);

        return Result<ItemData>.FromSuccess(slotStack with { Count = taken });
    }

    /// <summary>Returns a read-only span over the contiguous item store.</summary>
    public readonly ReadOnlySpan<ItemData> AsSpan()
    {
        if (_items != null)
        {
            return _items.AsSpan();
        }

        if (Slots == null || Slots.Length == 0)
        {
            return ReadOnlySpan<ItemData>.Empty;
        }

        InventoryComponent hydrated = Clone();
        return hydrated._items != null ? hydrated._items.AsSpan() : ReadOnlySpan<ItemData>.Empty;
    }

    /// <summary>Returns whether any slot or orphan entry holds a non-empty item stack without allocating.</summary>
    public readonly bool HasAnyItems()
    {
        if (_orphans != null && _orphans.Count > 0)
        {
            return true;
        }

        if (_items != null)
        {
            for (var i = 0; i < _items.Length; i++)
            {
                if (!_items[i].IsEmpty())
                {
                    return true;
                }
            }

            return false;
        }

        if (Slots == null)
        {
            return false;
        }

        for (var i = 0; i < Slots.Length; i++)
        {
            if (!Slots[i].Item.IsEmpty())
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns sparse non-empty slot entries (plus any unrecovered orphans) for persistence.</summary>
    public readonly KeyedSlotEntry[] ToSparseEntries()
    {
        if (_layout == null || _items == null)
        {
            return Slots != null ? [.. Slots] : [];
        }

        return BuildSparseSlots(_layout, _items, _orphans);
    }

    private void LoadSparseCore(ReadOnlySpan<KeyedSlotEntry> entries)
    {
        if (entries.Length == 0)
        {
            Slots = [];
            return;
        }

        List<KeyedSlotEntry>? orphans = null;
        for (var i = 0; i < entries.Length; i++)
        {
            KeyedSlotEntry entry = entries[i];
            if (entry.Item.IsEmpty())
            {
                continue;
            }

            ItemData item = entry.Item with { MaxSize = Math.Max(1, entry.Item.MaxSize) };
            var slot = new SlotId(entry.GroupId ?? string.Empty, entry.LocalIndex);
            if (_layout!.TryResolveIndex(in slot, out int index) && _items![index].IsEmpty())
            {
                _items[index] = item;
            }
            else
            {
                orphans ??= [];
                orphans.Add(new KeyedSlotEntry(entry.GroupId ?? string.Empty, entry.LocalIndex, item));
            }
        }

        if (orphans != null)
        {
            AppendOrphans(orphans);
        }

        SyncWireState(includeGroups: false);
    }

    private void AppendOrphans(List<KeyedSlotEntry> pending)
    {
        if (_orphans == null)
        {
            _orphans = pending;
        }
        else
        {
            _orphans.AddRange(pending);
        }
    }

    private void TryDrainOrphansIntoBackpack()
    {
        if (_orphans == null || _orphans.Count == 0)
        {
            return;
        }

        if (!_layout!.TryGetGroup(SlotGroups.BACKPACK, out SlotGroupRange backpack))
        {
            return;
        }

        for (var i = 0; i < _orphans.Count; i++)
        {
            KeyedSlotEntry orphan = _orphans[i];
            ItemData remaining = orphan.Item;

            StackIntoRange(backpack, ref remaining);
            if (remaining.Count > 0)
            {
                PlaceIntoEmptyRange(backpack, ref remaining);
            }

            if (remaining.Count <= 0)
            {
                _orphans.RemoveAt(i);
                i--;
            }
            else
            {
                _orphans[i] = new KeyedSlotEntry(orphan.GroupId, orphan.LocalIndex, remaining);
            }
        }

        if (_orphans.Count == 0)
        {
            _orphans = null;
        }
    }

    private bool AddToGroup(string groupId, ref ItemData remaining, bool onlyEmptySlots)
    {
        if (!_layout!.TryGetGroup(groupId, out SlotGroupRange range))
        {
            return false;
        }

        var changed = false;
        if (!onlyEmptySlots)
        {
            changed |= StackIntoRange(range, ref remaining);
        }

        if (remaining.Count > 0)
        {
            changed |= PlaceIntoEmptyRange(range, ref remaining);
        }

        return changed;
    }

    private bool StackIntoRange(in SlotGroupRange range, ref ItemData remaining)
    {
        var changed = false;
        for (var i = 0; i < range.Capacity && remaining.Count > 0; i++)
        {
            int index = range.Offset + i;
            ItemData slotStack = _items![index];
            if (slotStack.IsEmpty() || !string.Equals(slotStack.ID, remaining.ID, StringComparison.Ordinal))
            {
                continue;
            }

            int maxStack = Math.Max(1, Math.Min(slotStack.MaxSize, remaining.MaxSize));
            int available = maxStack - slotStack.Count;
            if (available <= 0)
            {
                continue;
            }

            int moved = Math.Min(remaining.Count, available);
            _items[index] = slotStack with { Count = slotStack.Count + moved, MaxSize = maxStack };
            remaining.Count -= moved;
            changed = true;
        }

        return changed;
    }

    private bool PlaceIntoEmptyRange(in SlotGroupRange range, ref ItemData remaining)
    {
        var changed = false;
        for (var i = 0; i < range.Capacity && remaining.Count > 0; i++)
        {
            int index = range.Offset + i;
            if (!_items![index].IsEmpty())
            {
                continue;
            }

            int maxStack = Math.Max(1, remaining.MaxSize);
            int placed = Math.Min(remaining.Count, maxStack);
            _items[index] = remaining with { Count = placed, MaxSize = maxStack };
            remaining.Count -= placed;
            changed = true;
        }

        return changed;
    }

    private void SyncWireState(bool includeGroups)
    {
        if (_orphans != null && _orphans.Count > 0)
        {
            TryDrainOrphansIntoBackpack();
        }

        if (includeGroups)
        {
            Groups = _layout!.ToDescriptors();
        }

        Slots = BuildSparseSlots(_layout!, _items!, _orphans);
    }

    private static KeyedSlotEntry[] BuildSparseSlots(
        SlotGroupLayout layout,
        ItemData[] items,
        List<KeyedSlotEntry>? orphans
    )
    {
        var count = orphans?.Count ?? 0;
        for (var i = 0; i < items.Length; i++)
        {
            if (!items[i].IsEmpty())
            {
                count++;
            }
        }

        if (count == 0)
        {
            return [];
        }

        var entries = new KeyedSlotEntry[count];
        var writeIndex = 0;

        for (var g = 0; g < layout.Groups.Count; g++)
        {
            SlotGroupRange range = layout.Groups[g];
            for (var i = 0; i < range.Capacity; i++)
            {
                ItemData item = items[range.Offset + i];
                if (!item.IsEmpty())
                {
                    entries[writeIndex++] = new KeyedSlotEntry(range.GroupId, i, item);
                }
            }
        }

        if (orphans != null)
        {
            for (var i = 0; i < orphans.Count; i++)
            {
                entries[writeIndex++] = orphans[i];
            }
        }

        return entries;
    }
}
