using System;
using System.Collections.Generic;
using Krill;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Components;
using WaywardBeyond.Items;
using WaywardBeyond.Server.Components;

namespace WaywardBeyond.Server.Systems;

/// <summary>Built-in server decorator that provisions keyed inventories and container access for bricks declaring slot groups.</summary>
public sealed class InventoryBrickEntityDecorator(
    ISlotGroupSchemaDatabase? schemas = null,
    IItemDatabase? itemDatabase = null
) : IServerBrickEntityDecorator
{
    /// <inheritdoc/>
    public bool AppliesTo(Brick brick) => brick.Entity?.HasInventory == true;

    /// <inheritdoc/>
    public void OnSpawn(DataStore store, int entity, Brick brick, in BrickEntityData? persisted)
    {
        if (brick.Entity is not { HasInventory: true } entityMeta)
        {
            return;
        }

        if (!store.TryGet(entity, out ContainerAccessComponent _))
        {
            store.AddOrUpdate(entity, new ContainerAccessComponent
            {
                MaxReach = CharacterDefaults.DEFAULT_REACH,
            });
        }

        InventoryComponent inventory = BuildInventory(entityMeta.SlotGroups, persisted?.Inventory);
        store.AddOrUpdate(entity, inventory);
    }

    private InventoryComponent BuildInventory(IReadOnlyList<BrickSlotGroup> slotGroups, KeyedSlotEntry[]? persistedEntries)
    {
        ISlotGroupSchemaDatabase resolvedSchemas = schemas ?? SlotGroupSchemaDatabase.Default;
        var descriptors = new SlotGroupDescriptor[slotGroups.Count];
        for (var i = 0; i < slotGroups.Count; i++)
        {
            BrickSlotGroup group = slotGroups[i];
            int capacity = group.Capacity ?? resolvedSchemas.GetCapacity(group.GroupId, GetDefaultGroupCapacity(group.GroupId));
            descriptors[i] = new SlotGroupDescriptor(group.GroupId, Math.Max(0, capacity));
        }

        var inventory = InventoryComponent.Create(descriptors);
        if (persistedEntries != null && persistedEntries.Length > 0)
        {
            var sanitized = new KeyedSlotEntry[persistedEntries.Length];
            for (var i = 0; i < persistedEntries.Length; i++)
            {
                KeyedSlotEntry entry = persistedEntries[i];
                sanitized[i] = new KeyedSlotEntry(entry.GroupId, entry.LocalIndex, ClampItemStack(entry.Item));
            }

            inventory.LoadSparse(sanitized);
        }

        return inventory;
    }

    private ItemData ClampItemStack(in ItemData item)
    {
        if (item.IsEmpty())
        {
            return default;
        }

        int maxSize = item.MaxSize > 0 ? item.MaxSize : 100;
        if (itemDatabase != null)
        {
            Result<Item> resolved = itemDatabase.Get(item.ID);
            if (resolved.Success && resolved.Value.MaxStack > 0)
            {
                maxSize = resolved.Value.MaxStack;
            }
        }

        int count = Math.Clamp(item.Count, 1, maxSize);
        return new ItemData(item.ID, count, maxSize);
    }

    private static int GetDefaultGroupCapacity(string groupId)
    {
        return groupId switch
        {
            SlotGroups.CARGO => SlotGroups.DEFAULT_CARGO_CAPACITY,
            SlotGroups.HOTBAR => SlotGroups.DEFAULT_HOTBAR_CAPACITY,
            SlotGroups.BACKPACK => SlotGroups.DEFAULT_BACKPACK_CAPACITY,
            SlotGroups.WEARABLES => SlotGroups.DEFAULT_WEARABLES_CAPACITY,
            SlotGroups.AUGMENTS => SlotGroups.DEFAULT_AUGMENTS_CAPACITY,
            _ => 1,
        };
    }
}
