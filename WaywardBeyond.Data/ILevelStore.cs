using System;
using System.Collections.Generic;

namespace WaywardBeyond.Data;

/// <summary>Provides access to a level's data.</summary>
public interface ILevelStore : IDisposable
{
    /// <summary></summary>
    byte[]? ReadLevel();

    IReadOnlyList<LevelEntityRecord> ReadEntities();

    byte[]? ReadLocation(ulong characterUuid);

    /// <summary>
    /// Commits a full snapshot in one transaction: upserts the level, replaces every entity row with
    /// <paramref name="entities"/>, and upserts each entry of <paramref name="locations"/> without
    /// deleting rows that are absent.
    /// </summary>
    void WriteSave(byte[] levelData, IReadOnlyList<LevelEntityRecord> entities, IReadOnlyList<LevelLocationRecord> locations);

    void WriteLevel(byte[] levelData);

    void WriteLocation(ulong characterUuid, byte[] data);
}
