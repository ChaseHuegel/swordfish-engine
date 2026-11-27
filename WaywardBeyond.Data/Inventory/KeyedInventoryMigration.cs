using System.Collections.Generic;

namespace WaywardBeyond.Data;

/// <summary>Converts legacy positional character inventories into sparse keyed slot entries.</summary>
public static class KeyedInventoryMigration
{
    /// <summary>
    /// Converts a legacy positional <see cref="ItemData"/> array into sparse keyed slot entries,
    /// mapping indices <c>0..8</c> to <see cref="SlotGroups.HOTBAR"/> and <c>9+</c> to
    /// <see cref="SlotGroups.BACKPACK"/>. Returns null when <paramref name="legacySlots"/> is null.
    /// </summary>
    public static KeyedSlotEntry[]? FromLegacySlots(ItemData[]? legacySlots)
    {
        if (legacySlots == null)
        {
            return null;
        }

        var entries = new List<KeyedSlotEntry>();
        for (var i = 0; i < legacySlots.Length; i++)
        {
            ItemData item = legacySlots[i];
            if (item.IsEmpty())
            {
                continue;
            }

            if (i < SlotGroups.DEFAULT_HOTBAR_CAPACITY)
            {
                entries.Add(new KeyedSlotEntry(SlotGroups.HOTBAR, i, item));
            }
            else
            {
                entries.Add(new KeyedSlotEntry(SlotGroups.BACKPACK, i - SlotGroups.DEFAULT_HOTBAR_CAPACITY, item));
            }
        }

        return entries.Count == 0 ? [] : [.. entries];
    }
}
