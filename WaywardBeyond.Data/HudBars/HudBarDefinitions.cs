using System.Collections.Generic;

namespace WaywardBeyond.Data.HudBars;

/// <summary>Parsed collection of HUD bar entries read from a TOML asset file.</summary>
internal sealed class HudBarDefinitions
{
    /// <summary>Parsed entries from the source file.</summary>
    internal List<HudBarEntry> Entries { get; } = [];
}
