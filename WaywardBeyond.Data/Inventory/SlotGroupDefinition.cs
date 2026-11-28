using System.Collections.Generic;

namespace WaywardBeyond.Data;

/// <summary>TOML definition of a keyed inventory slot group schema.</summary>
public struct SlotGroupDefinition()
{
    /// <summary>The canonical namespaced slot group identifier.</summary>
    public string ID = string.Empty;

    /// <summary>The default slot capacity of the group.</summary>
    public int Capacity;

    /// <summary>Allowed item invariant tag keys applied to any slot in the group without an override.</summary>
    public List<string>? AllowedTags;

    /// <summary>Per-slot allowed tag overrides keyed by local slot index.</summary>
    public List<SlotFilterDefinition>? Slots;
}
