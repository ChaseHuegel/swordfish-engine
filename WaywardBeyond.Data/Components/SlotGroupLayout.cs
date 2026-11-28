using System;
using System.Collections.Generic;

namespace WaywardBeyond.Data.Components;

/// <summary>Group-offset table mapping keyed slot groups onto a contiguous item store.</summary>
public sealed class SlotGroupLayout
{
    /// <summary>Maximum total slot capacity permitted across all groups in one layout.</summary>
    public const int MAX_TOTAL_CAPACITY = 4096;

    private readonly SlotGroupRange[] _ranges;
    private readonly Dictionary<string, int> _indexByGroupId;

    /// <summary>An empty layout with zero groups and zero total capacity.</summary>
    public static SlotGroupLayout Empty { get; } = new([]);

    private SlotGroupLayout(SlotGroupRange[] ranges)
    {
        _ranges = ranges;
        _indexByGroupId = new Dictionary<string, int>(ranges.Length, StringComparer.Ordinal);

        var total = 0;
        for (var i = 0; i < ranges.Length; i++)
        {
            SlotGroupRange range = ranges[i];
            _indexByGroupId[range.GroupId] = i;
            total += range.Capacity;
        }

        TotalCapacity = total;
    }

    /// <summary>The ordered group ranges forming the contiguous backing store.</summary>
    public IReadOnlyList<SlotGroupRange> Groups => _ranges;

    /// <summary>Zero-allocation span view of the ordered group ranges.</summary>
    public ReadOnlySpan<SlotGroupRange> GroupSpan => _ranges;

    /// <summary>The total number of slots across all groups in this layout.</summary>
    public int TotalCapacity { get; }

    /// <summary>Creates a layout from explicit group descriptors, deduplicating group IDs in first-seen order.</summary>
    public static SlotGroupLayout Create(ReadOnlySpan<SlotGroupDescriptor> descriptors)
    {
        if (descriptors.Length == 0)
        {
            return Empty;
        }

        var ranges = new List<SlotGroupRange>(descriptors.Length);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var offset = 0;

        for (var i = 0; i < descriptors.Length; i++)
        {
            SlotGroupDescriptor descriptor = descriptors[i];
            if (string.IsNullOrEmpty(descriptor.GroupId) || descriptor.Capacity < 0 || !seen.Add(descriptor.GroupId))
            {
                continue;
            }

            if (descriptor.Capacity > MAX_TOTAL_CAPACITY - offset)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(descriptors),
                    descriptor.Capacity,
                    $"Inventory total capacity exceeds the maximum of {MAX_TOTAL_CAPACITY} slots.");
            }

            ranges.Add(new SlotGroupRange(descriptor.GroupId, offset, descriptor.Capacity));
            offset += descriptor.Capacity;
        }

        return ranges.Count == 0 ? Empty : new SlotGroupLayout([.. ranges]);
    }

    /// <summary>Builds a layout from wire descriptors, returning <see cref="Empty"/> when null or empty.</summary>
    public static SlotGroupLayout FromDescriptors(SlotGroupDescriptor[]? descriptors)
    {
        return descriptors == null || descriptors.Length == 0
            ? Empty
            : Create(descriptors);
    }

    /// <summary>Creates the canonical player layout using capacities from <paramref name="schemas"/> when supplied.</summary>
    public static SlotGroupLayout CreatePlayer(ISlotGroupSchemaDatabase? schemas = null)
    {
        ISlotGroupSchemaDatabase resolved = schemas ?? SlotGroupSchemaDatabase.Default;
        return Create(
        [
            new SlotGroupDescriptor(SlotGroups.HOTBAR, resolved.GetCapacity(SlotGroups.HOTBAR, SlotGroups.DEFAULT_HOTBAR_CAPACITY)),
            new SlotGroupDescriptor(SlotGroups.BACKPACK, resolved.GetCapacity(SlotGroups.BACKPACK, SlotGroups.DEFAULT_BACKPACK_CAPACITY)),
            new SlotGroupDescriptor(SlotGroups.WEARABLES, resolved.GetCapacity(SlotGroups.WEARABLES, SlotGroups.DEFAULT_WEARABLES_CAPACITY)),
            new SlotGroupDescriptor(SlotGroups.AUGMENTS, resolved.GetCapacity(SlotGroups.AUGMENTS, SlotGroups.DEFAULT_AUGMENTS_CAPACITY)),
        ]);
    }

    /// <summary>Resolves the contiguous offset and capacity for <paramref name="groupId"/>.</summary>
    public bool TryGetGroup(string? groupId, out SlotGroupRange range)
    {
        if (!string.IsNullOrEmpty(groupId) && _indexByGroupId.TryGetValue(groupId, out int index))
        {
            range = _ranges[index];
            return true;
        }

        range = default;
        return false;
    }

    /// <summary>Returns the slot capacity of <paramref name="groupId"/>, or zero when absent.</summary>
    public int GetGroupCapacity(string? groupId)
    {
        return TryGetGroup(groupId, out SlotGroupRange range) ? range.Capacity : 0;
    }

    /// <summary>Resolves a composite <see cref="SlotId"/> to its flat index in the contiguous store.</summary>
    public bool TryResolveIndex(in SlotId slot, out int flatIndex)
    {
        if (!string.IsNullOrEmpty(slot.GroupId)
            && slot.LocalIndex >= 0
            && _indexByGroupId.TryGetValue(slot.GroupId, out int index))
        {
            SlotGroupRange range = _ranges[index];
            if (slot.LocalIndex < range.Capacity)
            {
                flatIndex = range.Offset + slot.LocalIndex;
                return true;
            }
        }

        flatIndex = -1;
        return false;
    }

    /// <summary>Returns a new layout with <paramref name="groupId"/> resized or appended at <paramref name="newCapacity"/>.</summary>
    public SlotGroupLayout WithGroupCapacity(string groupId, int newCapacity)
    {
        if (string.IsNullOrEmpty(groupId))
        {
            throw new ArgumentException("Group ID cannot be null or empty.", nameof(groupId));
        }

        int clampedCapacity = Math.Max(0, newCapacity);
        bool exists = _indexByGroupId.TryGetValue(groupId, out int existingIndex);
        int count = exists ? _ranges.Length : _ranges.Length + 1;
        var next = new SlotGroupRange[count];
        var offset = 0;

        for (var i = 0; i < _ranges.Length; i++)
        {
            SlotGroupRange current = _ranges[i];
            int capacity = i == existingIndex ? clampedCapacity : current.Capacity;
            if (capacity > MAX_TOTAL_CAPACITY - offset)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(newCapacity),
                    newCapacity,
                    $"Inventory total capacity exceeds the maximum of {MAX_TOTAL_CAPACITY} slots.");
            }

            next[i] = new SlotGroupRange(current.GroupId, offset, capacity);
            offset += capacity;
        }

        if (!exists)
        {
            if (clampedCapacity > MAX_TOTAL_CAPACITY - offset)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(newCapacity),
                    newCapacity,
                    $"Inventory total capacity exceeds the maximum of {MAX_TOTAL_CAPACITY} slots.");
            }

            next[_ranges.Length] = new SlotGroupRange(groupId, offset, clampedCapacity);
        }

        return new SlotGroupLayout(next);
    }

    /// <summary>Converts this layout to wire descriptors.</summary>
    public SlotGroupDescriptor[] ToDescriptors()
    {
        if (_ranges.Length == 0)
        {
            return [];
        }

        var descriptors = new SlotGroupDescriptor[_ranges.Length];
        for (var i = 0; i < _ranges.Length; i++)
        {
            SlotGroupRange range = _ranges[i];
            descriptors[i] = new SlotGroupDescriptor(range.GroupId, range.Capacity);
        }

        return descriptors;
    }
}
