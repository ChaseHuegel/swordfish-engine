namespace WaywardBeyond.Data.Buffs;

/// <summary>TOML schema for a stat modifier entry on a buff.</summary>
public struct BuffStatModifierDefinition()
{
    /// <summary>Target statistic identifier.</summary>
    public string? StatId { get; set; }

    /// <summary>Optional additive flat delta applied while active.</summary>
    public float? FlatDelta { get; set; }

    /// <summary>Optional percentage delta applied while active.</summary>
    public float? PercentDelta { get; set; }
}
