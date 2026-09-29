namespace WaywardBeyond.Shared.Data;

/// <summary>
/// Single source of truth for the current game-save data format version. Every save-bearing record
/// (<see cref="Character"/>, <see cref="Level"/>) stamps this value, and <see cref="SaveMigrator"/>
/// uses it as the target when migrating older saves forward. Bump this only when the serialized save
/// format changes in a way that needs a data migration.
/// </summary>
public static class SaveVersion
{
    /// <summary>Indexes 0 and up are reserved: 0 is the empty/air voxel, so registry ids begin at 1.</summary>
    public const ushort MinDataId = 1;

    /// <summary>The data format version stamped into new saves and targeted by migrations.</summary>
    public const uint CurrentDataVersion = 4;
}