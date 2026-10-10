using System;

namespace WaywardBeyond.Data.Migrations;

/// <summary>
///     An interface representing a single forward migration of a data record from one schema version to the next.
///     Migrations run in ascending <see cref="FromVersion"/> order up to <see cref="SaveVersion.CURRENT_DATA_VERSION"/>.
/// </summary>
public interface IMigration
{
    /// <summary>The data schema version this migration reads as input.</summary>
    uint FromVersion { get; }

    /// <summary>The data schema version this migration produces.</summary>
    uint ToVersion { get; }

    /// <summary>The record type this migration transforms.</summary>
    Type TargetType { get; }

    /// <summary>
    ///     Performs a deterministic and idempotent transformation of a record
    ///     from <see cref="FromVersion"/>'s schema to <see cref="ToVersion"/>'s schema.
    /// </summary>
    object Apply(object value);
}