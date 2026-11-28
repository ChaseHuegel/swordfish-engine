using System.Collections.Generic;

namespace WaywardBeyond.Items;

/// <summary>Defines wearable or augment equipment slot metadata and stat modifiers for an item.</summary>
public struct EquipDefinition()
{
    /// <summary>Slot tag identifying the compatible wearable slot or augment socket.</summary>
    public string? SlotTag;

    /// <summary>Statistic modifiers granted while the item is equipped.</summary>
    public List<EquipStatModifierDefinition>? StatModifiers;
}
