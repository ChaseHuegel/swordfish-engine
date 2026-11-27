using System.Collections.Generic;

namespace WaywardBeyond.Data.Buffs;

/// <summary>Parsed collection of buff entries read from a TOML asset file.</summary>
public sealed class BuffDefinitions
{
    /// <summary>Parsed entries from the source file.</summary>
    internal List<BuffEntry> Entries { get; } = [];
}
