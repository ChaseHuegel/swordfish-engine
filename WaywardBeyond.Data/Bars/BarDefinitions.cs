using System.Collections.Generic;

namespace WaywardBeyond.Data.Bars;

/// <summary>A collection of <see cref="BarDefinition"/> entries parsed from TOML.</summary>
public struct BarDefinitions()
{
    /// <summary>The contained <see cref="BarDefinition"/> entries.</summary>
    public List<BarDefinition>? Bars { get; set; }
}
