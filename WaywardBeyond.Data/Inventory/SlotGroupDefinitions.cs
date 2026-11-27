using System.Collections.Generic;

namespace WaywardBeyond.Data;

/// <summary>TOML collection of <see cref="SlotGroupDefinition"/> entries.</summary>
public struct SlotGroupDefinitions()
{
    /// <summary>The slot group definitions contained in the file.</summary>
    public List<SlotGroupDefinition> SlotGroups = [];
}
