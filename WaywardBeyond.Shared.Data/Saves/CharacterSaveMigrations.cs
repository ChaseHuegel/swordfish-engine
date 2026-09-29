namespace WaywardBeyond.Shared.Data;

/// <summary>
/// The standard data-format migrator for character records. The character record is unchanged between
/// data versions 3 and 4 (its namespacing lands with a later content change), so the chain is currently
/// empty and older, same-shaped records pass through; the <see cref="SaveMigrator"/> version gate still
/// refuses characters stamped by a newer build.
/// </summary>
internal static class CharacterSaveMigrations
{
    internal static SaveMigrator Create()
    {
        return new SaveMigrator([]);
    }
}