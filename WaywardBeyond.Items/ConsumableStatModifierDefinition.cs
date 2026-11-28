namespace WaywardBeyond.Items;

/// <summary>Defines one statistic change applied once when a consumable is used.</summary>
public struct ConsumableStatModifierDefinition()
{
    /// <summary>Target statistic identifier.</summary>
    public string? StatId;

    /// <summary>Optional additive flat delta.</summary>
    public float? FlatDelta;

    /// <summary>Optional percentage delta applied to the current value.</summary>
    public float? PercentDelta;
}
