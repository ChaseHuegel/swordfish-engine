using System.Collections.Generic;

namespace WaywardBeyond.Data;

/// <summary>TOML definition of a per-slot filter override inside a slot group.</summary>
public struct SlotFilterDefinition()
{
    /// <summary>The local slot index within the owning group.</summary>
    public int Index;

    /// <summary>Allowed item invariant tag keys for this slot index.</summary>
    public List<string>? AllowedTags;
}
