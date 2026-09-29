using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// Data version 3 to 4 migration for world voxel structures. Version 3 written structures carry raw
/// FNV1a voxel ids and no brick palette; version 4 carries a <see cref="BaseBrickCatalog"/> palette so
/// saved ids are stable and self-describing. Re-indexing legacy ids into the palette is idempotent, so a
/// partially-migrated structure (or one already re-encoded) can be safely re-processed.
/// </summary>
public sealed class VoxelEntityDataV3ToV4Migration : SaveMigration<VoxelEntityData>
{
    public override uint FromVersion => 3;
    public override uint ToVersion => 4;

    public override VoxelEntityData ApplyValue(VoxelEntityData value)
    {
        return VoxelEntityDataCodec.EncodeLegacyToPalette(in value);
    }
}

/// <summary>
/// The standard data-format migrator for game-save records. Registers every forward migration shipping
/// with this build. Provides the single <see cref="SaveMigrator"/> used by the persistence paths.
/// </summary>
public static class GameSaveMigrations
{
    /// <summary>A migrator carrying every game-save record migration for this build.</summary>
    public static SaveMigrator Migrator { get; } = new(new ISaveMigration[]
    {
        new VoxelEntityDataV3ToV4Migration(),
    });
}