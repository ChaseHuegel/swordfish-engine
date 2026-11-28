using System.Collections.Generic;
using Swordfish.Library.Collections;

namespace WaywardBeyond.Data.HudBars;

/// <summary>Provides access to loaded <see cref="HudBar"/> assets.</summary>
public interface IHudBarDatabase : IAssetDatabase<HudBar>
{
    /// <summary>Gets all loaded HUD bars sorted by display order and ID.</summary>
    IReadOnlyList<HudBar> All { get; }
}
