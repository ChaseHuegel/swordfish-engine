using System.Collections.Generic;

namespace WaywardBeyond.Data.Buffs;

/// <summary>A collection of <see cref="BuffDefinition"/> entries parsed from TOML.</summary>
public struct BuffDefinitions()
{
    /// <summary>The contained <see cref="BuffDefinition"/> entries.</summary>
    public List<BuffDefinition>? Buffs { get; set; }
}
