using System.Collections.Generic;
using Swordfish.Library.Collections;

namespace WaywardBeyond.Data.HudBars;

/// <summary>Provides access to loaded <see cref="HudBarDefinition"/> assets.</summary>
public interface IHudBarDatabase : IAssetDatabase<HudBarDefinition>
{
    /// <summary>Gets all loaded HUD bar definitions sorted by display order and ID.</summary>
    IReadOnlyList<HudBarDefinition> All { get; }
}
