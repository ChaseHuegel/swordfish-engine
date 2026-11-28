namespace WaywardBeyond.Data.Buffs;

/// <summary>TOML schema for a per-second periodic stat change entry on a buff.</summary>
public struct BuffRegenRateDefinition()
{
    /// <summary>Target statistic identifier (for example <c>wb:health</c> or <c>wb:oxygen</c>).</summary>
    public string? StatId { get; set; }

    /// <summary>Units added (positive) or drained (negative) per second while active.</summary>
    public float PerSecond { get; set; }
}
