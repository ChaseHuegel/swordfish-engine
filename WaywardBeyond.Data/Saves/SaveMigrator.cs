using System;
using System.Collections.Generic;
using System.Linq;

namespace WaywardBeyond.Data;

/// <summary>
/// Applies forward data-format migrations to save-bearing records. Holds the ordered chain of
/// <see cref="ISaveMigration"/>s per record type and gates on <see cref="SaveVersion.CurrentDataVersion"/>:
/// a record at current version passes through unchanged, one at an older version runs each step up to
/// current, and one at a newer version is refused via <see cref="SaveDataNotSupportedException"/>.
/// </summary>
public sealed class SaveMigrator
{
    /// <summary>The data format version every migration chain targets.</summary>
    public uint CurrentDataVersion => SaveVersion.CurrentDataVersion;

    private readonly IReadOnlyDictionary<Type, List<ISaveMigration>> _migrations;

    public SaveMigrator(IEnumerable<ISaveMigration> migrations)
    {
        _migrations = migrations
            .GroupBy(migration => migration.TargetType)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(migration => migration.FromVersion).ToList()
            );

        foreach ((Type type, List<ISaveMigration> chain) in _migrations)
        {
            ValidateChain(type, chain);
        }
    }

    /// <summary>Whether a record stamped at <paramref name="fromVersion"/> can be loaded (not newer than current).</summary>
    public bool IsSupported(uint fromVersion)
    {
        return fromVersion <= SaveVersion.CurrentDataVersion;
    }

    /// <summary>
    /// Migrates a record stamped at <paramref name="fromVersion"/> up to the current data format version.
    /// Throws <see cref="SaveDataNotSupportedException"/> when the record is newer than this build.
    /// </summary>
    public T Migrate<T>(T value, uint fromVersion)
    {
        if (fromVersion > SaveVersion.CurrentDataVersion)
        {
            throw new SaveDataNotSupportedException(fromVersion, SaveVersion.CurrentDataVersion);
        }

        if (!_migrations.TryGetValue(typeof(T), out List<ISaveMigration>? chain) || chain.Count == 0)
        {
            //  No migration steps for this type: its format did not change in this version, so an older,
            //  same-shaped record loads unchanged.
            return value;
        }

        object current = value;
        foreach (ISaveMigration migration in chain)
        {
            if (fromVersion >= migration.ToVersion)
            {
                continue;
            }

            current = migration.Apply(current);
        }

        return (T)current;
    }

    private static void ValidateChain(Type type, List<ISaveMigration> chain)
    {
        if (chain.Count == 0)
        {
            return;
        }

        //  Each step must advance exactly one version, and the chain must reach the current version.
        uint expected = chain[0].FromVersion;
        foreach (ISaveMigration migration in chain)
        {
            if (migration.FromVersion != expected || migration.ToVersion != expected + 1)
            {
                throw new InvalidOperationException(
                    $"Migration chain for {type.Name} must advance exactly one version at a time.");
            }

            expected = migration.ToVersion;
        }

        if (expected != SaveVersion.CurrentDataVersion)
        {
            throw new InvalidOperationException(
                $"Migration chain for {type.Name} ends at {expected}, not the current version {SaveVersion.CurrentDataVersion}.");
        }
    }
}