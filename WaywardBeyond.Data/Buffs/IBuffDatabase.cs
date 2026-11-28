using System.Collections.Generic;
using Swordfish.Library.Collections;

namespace WaywardBeyond.Data.Buffs;

/// <summary>Provides access to loaded <see cref="Buff"/> assets.</summary>
public interface IBuffDatabase : IAssetDatabase<Buff>
{
    /// <summary>Gets all loaded buffs sorted deterministically by ID.</summary>
    IReadOnlyList<Buff> All { get; }
}
