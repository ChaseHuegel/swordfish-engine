using System;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Items;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>Validation context supplied when resolving keyed inventory transfers.</summary>
public readonly struct InventoryValidationContext
{
    /// <summary>Shared default context using built-in schemas without tag lookup or custom stack limits.</summary>
    public static InventoryValidationContext Default { get; } = new(SlotGroupSchemaDatabase.Default, null, null);

    /// <summary>Constructs a validation context with optional schemas, invariant tags, and item definitions.</summary>
    public InventoryValidationContext(
        ISlotGroupSchemaDatabase? schemas = null,
        LocalizedTags? invariantTags = null,
        IAssetDatabase<Item>? items = null
    )
    {
        Schemas = schemas ?? SlotGroupSchemaDatabase.Default;
        InvariantTags = invariantTags;
        Items = items;
    }

    /// <summary>Slot group schema database used to enforce group and per-slot filter tags.</summary>
    public ISlotGroupSchemaDatabase Schemas { get; }

    /// <summary>Optional invariant item tag registry used when evaluating slot filter tags.</summary>
    public LocalizedTags? InvariantTags { get; }

    /// <summary>Optional item database supplying authoritative stack limits.</summary>
    public IAssetDatabase<Item>? Items { get; }

    /// <summary>Returns the effective max stack size clamped to any authoritative item definition.</summary>
    public int ResolveMaxStack(in ItemData item)
    {
        int itemMax = Math.Max(1, item.MaxSize);
        if (Items == null || string.IsNullOrEmpty(item.ID))
        {
            return itemMax;
        }

        Result<Item> definition = Items.Get(item.ID);
        return definition.Success && definition.Value.MaxStack > 0
            ? Math.Min(itemMax, definition.Value.MaxStack)
            : itemMax;
    }

    /// <summary>Returns the effective max stack size when merging <paramref name="source"/> into <paramref name="destination"/>.</summary>
    public int ResolveMergeMaxStack(in ItemData destination, in ItemData source)
    {
        return Math.Min(Math.Max(1, destination.MaxSize), ResolveMaxStack(in source));
    }

    /// <summary>Returns true when <paramref name="slot"/> accepts <paramref name="item"/>.</summary>
    public bool AllowsItem(in SlotId slot, in ItemData item)
    {
        return Schemas.AllowsItem(in slot, item.ID, InvariantTags);
    }
}
