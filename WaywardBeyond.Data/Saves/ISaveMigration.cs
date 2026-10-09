using System;

namespace WaywardBeyond.Data;

/// <summary>
/// A single forward migration of a save-bearing record from one data format version to the next.
/// Migrations run in ascending <see cref="FromVersion"/> order up to
/// <see cref="SaveVersion.CurrentDataVersion"/>. Apply must be deterministic and idempotent so a
/// partially-migrated record can be safely re-processed.
/// </summary>
public interface ISaveMigration
{
    /// <summary>The data format version this migration reads as input.</summary>
    uint FromVersion { get; }

    /// <summary>The data format version this migration produces; must equal FromVersion + 1.</summary>
    uint ToVersion { get; }

    /// <summary>The record type this migration transforms.</summary>
    Type TargetType { get; }

    /// <summary>Transforms a record from <see cref="FromVersion"/> form to <see cref="ToVersion"/> form.</summary>
    object Apply(object value);
}

/// <summary>
/// Strongly-typed base for a <see cref="ISaveMigration"/> over a specific record type <typeparamref name="T"/>.
/// </summary>
public abstract class SaveMigration<T> : ISaveMigration
{
    public abstract uint FromVersion { get; }
    public abstract uint ToVersion { get; }
    public Type TargetType => typeof(T);
    public abstract T ApplyValue(T value);
    public object Apply(object value) => ApplyValue((T)value);
}

/// <summary>
/// Thrown when opening a save stamped with a newer data format version than this build understands.
/// The load must refuse rather than risk misreading newer data.
/// </summary>
public sealed class SaveDataNotSupportedException : Exception
{
    public uint StampedVersion { get; }
    public uint CurrentVersion { get; }

    public SaveDataNotSupportedException(uint stampedVersion, uint currentVersion)
        : base($"Save data version {stampedVersion} is newer than the supported format version {currentVersion}.")
    {
        StampedVersion = stampedVersion;
        CurrentVersion = currentVersion;
    }
}