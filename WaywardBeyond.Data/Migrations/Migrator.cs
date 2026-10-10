using System;
using System.Collections.Generic;
using System.Linq;

namespace WaywardBeyond.Data.Migrations;

/// <summary>Applies forward data <see cref="IMigration"/>s to records up to the <see cref="SaveVersion.CURRENT_DATA_VERSION"/>.</summary>
public sealed class Migrator
{
    private readonly Dictionary<Type, List<IMigration>> _migrations;

    public Migrator(IEnumerable<IMigration> migrations)
    {
        _migrations = migrations
            .GroupBy(migration => migration.TargetType)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(migration => migration.FromVersion).ToList()
            );

        foreach ((Type type, List<IMigration> chain) in _migrations)
        {
            ValidateChain(type, chain);
        }
    }

    /// <summary>Whether a record stamped at <paramref name="fromVersion"/> can be migrated.</summary>
    public bool IsSupported<T>(uint fromVersion)
    {
        return IsSupported(typeof(T), fromVersion);
    }
    
    /// <summary>Whether a record stamped at <paramref name="fromVersion"/> can be migrated.</summary>
    public bool IsSupported(Type type, uint fromVersion)
    {
        if (!_migrations.TryGetValue(type, out List<IMigration>? chain) || chain.Count == 0)
        {
            //  No migration steps for this type. It is unchanged.
            return true;
        }

        if (fromVersion < chain[0].FromVersion)
        {
            //  The oldest migration for this type does not support the provided version.
            return false;
        }
        
        return fromVersion <= SaveVersion.CURRENT_DATA_VERSION;
    }

    /// <summary>Migrates a record stamped at <paramref name="fromVersion"/> up to the <see cref="SaveVersion.CURRENT_DATA_VERSION"/>.</summary>
    /// <exception cref="NotSupportedException">Thrown when the record is out of range of the migrations' supported versions.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public T Migrate<T>(T value, uint fromVersion)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        
        if (fromVersion > SaveVersion.CURRENT_DATA_VERSION)
        {
            throw new NotSupportedException($"Record of type {typeof(T)} at version {fromVersion} is newer than the current version (version {SaveVersion.CURRENT_DATA_VERSION}) and cannot be safely migrated.");
        }

        if (!_migrations.TryGetValue(typeof(T), out List<IMigration>? chain) || chain.Count == 0)
        {
            //  No migration steps for this type. It is unchanged.
            return value;
        }

        if (fromVersion < chain[0].FromVersion)
        {
            throw new NotSupportedException($"Record of type {typeof(T)} at version {fromVersion} predates the earliest migration (version {chain[0].FromVersion}) and cannot be safely migrated.");
        }

        object current = value;
        foreach (IMigration migration in chain)
        {
            if (fromVersion >= migration.ToVersion)
            {
                continue;
            }

            current = migration.Apply(current);
        }

        return (T)current;
    }

    private static void ValidateChain(Type type, List<IMigration> chain)
    {
        if (chain.Count == 0)
        {
            return;
        }

        //  Each step must advance exactly one version, and the chain must reach the current version.
        uint expected = chain[0].FromVersion;
        foreach (IMigration migration in chain)
        {
            if (migration.FromVersion != expected || migration.ToVersion != expected + 1)
            {
                throw new InvalidOperationException($"Migration chain for {type.Name} must advance exactly one version at a time.");
            }

            expected = migration.ToVersion;
        }

        if (expected != SaveVersion.CURRENT_DATA_VERSION)
        {
            throw new InvalidOperationException($"Migration chain for {type.Name} ends at {expected}, not the current version {SaveVersion.CURRENT_DATA_VERSION}.");
        }
    }
}