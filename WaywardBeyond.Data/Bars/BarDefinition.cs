namespace WaywardBeyond.Data.Bars;

/// <summary>TOML schema for a data-driven icon bar definition.</summary>
public struct BarDefinition()
{
    /// <summary>Unique namespaced bar ID (for example <c>wb:health</c>).</summary>
    public string? ID { get; set; }

    /// <summary>Stat ID providing the bar's current value.</summary>
    public string? StatId { get; set; }

    /// <summary>Optional stat ID providing the bar's maximum value.</summary>
    public string? MaxStatId { get; set; }

    /// <summary>Optional fallback maximum used when <see cref="MaxStatId"/> is omitted or absent.</summary>
    public float? DefaultMax { get; set; }

    /// <summary>Optional number of discrete icons rendered across the bar (defaults to <see cref="Bar.DEFAULT_ICON_COUNT"/>).</summary>
    public int? IconCount { get; set; }

    /// <summary>Texture asset key for a full icon step.</summary>
    public string? IconFull { get; set; }

    /// <summary>Texture asset key for a half icon step.</summary>
    public string? IconHalf { get; set; }

    /// <summary>Texture asset key for an empty icon step.</summary>
    public string? IconEmpty { get; set; }

    /// <summary>Optional normalized ratio threshold at or below which warning state activates.</summary>
    public float? WarnThreshold { get; set; }

    /// <summary>Optional normalized ratio threshold at or below which critical state activates.</summary>
    public float? CriticalThreshold { get; set; }

    /// <summary>Vertical stacking order (lower values render first).</summary>
    public int Order { get; set; }

    /// <summary>Whether the bar hides while the player is in Creative mode.</summary>
    public bool HideInCreative { get; set; }

    /// <summary>Optional hex color tint string.</summary>
    public string? ColorTint { get; set; }
}
