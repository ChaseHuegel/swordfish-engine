using System;
using System.Collections.Generic;

namespace WaywardBeyond.Data;

/// <summary>
/// One level's save database: level metadata, voxel entities, and per-character locations. Records are
/// raw nsd blobs; the caller owns serialization and migration.
/// </summary>
public interface ILevelStore : IDisposable
{
    byte[]? ReadLevel();

    IReadOnlyList<LevelEntityRecord> ReadEntities();

    byte[]? ReadLocation(ulong characterId);

    /// <summary>
    /// Commits a full snapshot in one transaction: upserts the level, replaces every entity row with
    /// <paramref name="entities"/>, and upserts each entry of <paramref name="locations"/> without
    /// deleting rows that are absent.
    /// </summary>
    void WriteSave(byte[] levelData, IReadOnlyList<LevelEntityRecord> entities, IReadOnlyList<LevelLocationRecord> locations);

    void WriteLevel(byte[] levelData);

    void WriteLocation(ulong characterId, byte[] data);
}
