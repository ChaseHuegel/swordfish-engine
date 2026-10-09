namespace WaywardBeyond.Data;

/// <summary>
/// The standard data-format migrator for character records. The character format changed in v4 (Body
/// became a string asset ID, not an appearance index), but the change is not forward-migrated: the game
/// is not released, so older records are left as-is and an unknown body ID falls back to the first loaded
/// body at resolution. The chain is empty and same-shaped older records pass through; the
/// <see cref="SaveMigrator"/> version gate still refuses characters stamped by a newer build.
/// </summary>
internal static class CharacterSaveMigrations
{
    internal static SaveMigrator Create()
    {
        return new SaveMigrator([]);
    }
}