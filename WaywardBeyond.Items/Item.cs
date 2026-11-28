using System;
using System.Collections.Generic;

namespace WaywardBeyond.Items;

/// <summary>
/// The headless view of a loaded item: identity, stacking, statistics, and the content it places or targets.
/// Render assets stay client-side; the item carries only the ids of its icon and model.
/// </summary>
public sealed class Item(
    string id,
    string name,
    string? iconID,
    int maxStack,
    PlaceableDefinition? placeable,
    ToolDefinition? tool,
    ModelDefinition? viewModel,
    ModelDefinition? worldModel,
    string? description = null,
    IReadOnlyDictionary<string, float>? statistics = null,
    ConsumableDefinition? consumable = null,
    EquipDefinition? equip = null
) {
    private static readonly IReadOnlyDictionary<string, float> _noStatistics =
        new Dictionary<string, float>(StringComparer.Ordinal);

    /// <summary>The canonical namespaced item id.</summary>
    public readonly string ID = id;

    /// <summary>The localization key of the display name.</summary>
    public readonly string Name = name;

    /// <summary>The localization key of the description, or null when the item has none.</summary>
    public readonly string? Description = description;

    /// <summary>The texture id of the inventory icon, or null when the item has none.</summary>
    public readonly string? IconID = iconID;

    /// <summary>The largest stack one slot holds.</summary>
    public readonly int MaxStack = maxStack;

    /// <summary>The content the item places, or null when it is not placeable.</summary>
    public readonly PlaceableDefinition? Placeable = placeable;

    /// <summary>The tool behavior the item carries, or null when it is not a tool.</summary>
    public readonly ToolDefinition? Tool = tool;

    /// <summary>The survival effect of consuming the item, or null when it is not consumable.</summary>
    public readonly ConsumableDefinition? Consumable = consumable;

    /// <summary>The equipment slot tag and stat modifiers, or null when the item cannot be equipped.</summary>
    public readonly EquipDefinition? Equip = equip;

    /// <summary>The view-model render ids, or null when the item has none.</summary>
    public readonly ModelDefinition? ViewModel = viewModel;

    /// <summary>The world-model render ids, or null when the item has none.</summary>
    public readonly ModelDefinition? WorldModel = worldModel;

    /// <summary>Custom numeric statistics declared on this item, or empty when none are declared.</summary>
    public readonly IReadOnlyDictionary<string, float> Statistics = statistics ?? _noStatistics;

    /// <summary>Gets the declared statistic value for <paramref name="statisticId"/>, or <paramref name="defaultValue"/>.</summary>
    public float GetStatistic(string statisticId, float defaultValue = 0f)
    {
        return TryGetStatistic(statisticId, out float value) ? value : defaultValue;
    }

    /// <summary>Attempts to read the declared statistic value for <paramref name="statisticId"/>.</summary>
    public bool TryGetStatistic(string statisticId, out float value)
    {
        if (string.IsNullOrEmpty(statisticId))
        {
            value = 0f;
            return false;
        }

        return Statistics.TryGetValue(statisticId, out value);
    }
}
