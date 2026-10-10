using WaywardBeyond.Data.Migrations;

namespace WaywardBeyond.Data;

/// <summary>
/// Single source of truth for the current game-save data format version. Every save-bearing record
/// (<see cref="Character"/>, <see cref="Level"/>) stamps this value, and <see cref="Migrator"/>
/// uses it as the target when migrating older saves forward. This is only bumped when the serialized
/// save format changes in a breaking way that needs a data migration.
/// </summary>
public static class SaveVersion
{
    /// <summary>The data format version stamped into new saves and targeted by migrations.</summary>
    public const uint CURRENT_DATA_VERSION = 4;
}