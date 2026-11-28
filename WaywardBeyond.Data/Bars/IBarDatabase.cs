using System.Collections.Generic;
using Swordfish.Library.Collections;

namespace WaywardBeyond.Data.Bars;

/// <summary>Provides access to loaded <see cref="Bar"/> assets.</summary>
public interface IBarDatabase : IAssetDatabase<Bar>
{
    /// <summary>Gets all loaded bars sorted by display order and ID.</summary>
    IReadOnlyList<Bar> All { get; }
}
