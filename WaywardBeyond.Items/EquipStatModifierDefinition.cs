namespace WaywardBeyond.Items;

/// <summary>Defines one statistic modifier granted while an item is equipped.</summary>
public struct EquipStatModifierDefinition()
{
    /// <summary>Target statistic identifier.</summary>
    public string? StatId;

    /// <summary>Optional additive flat delta.</summary>
    public float? FlatDelta;

    /// <summary>Optional percentage delta applied to the base total.</summary>
    public float? PercentDelta;
}
