using System.Collections.Generic;

namespace WaywardBeyond.Items;

/// <summary>Defines the survival effect of consuming one item.</summary>
public struct ConsumableDefinition()
{
    /// <summary>Optional buff granted on use.</summary>
    public string? BuffID;

    /// <summary>The minimum seconds between uses of the item.</summary>
    public float CooldownSeconds;

    /// <summary>Statistic changes applied once on use, in declaration order.</summary>
    public List<ConsumableStatModifierDefinition>? Modifiers;
}
