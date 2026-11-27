using System.Collections.Generic;
using Swordfish.Library.Collections;

namespace WaywardBeyond.Data.Buffs;

/// <summary>Provides access to loaded <see cref="BuffDefinition"/> assets.</summary>
public interface IBuffDatabase : IAssetDatabase<BuffDefinition>
{
    /// <summary>Gets all loaded buff definitions sorted deterministically by ID.</summary>
    IReadOnlyList<BuffDefinition> All { get; }
}
