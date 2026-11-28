using System.Collections.Generic;

namespace WaywardBeyond.Data.HudBars;

/// <summary>A collection of <see cref="HudBarDefinition"/> entries parsed from TOML.</summary>
public struct HudBarDefinitions()
{
    /// <summary>The contained <see cref="HudBarDefinition"/> entries.</summary>
    public List<HudBarDefinition>? HudBars { get; set; }
}
