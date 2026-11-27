namespace WaywardBeyond.Data.Migrations;

/// <summary>Migrates version 4 positional character inventories to version 5 sparse keyed entries.</summary>
public sealed class CharacterKeyedInventoryMigration : Migration<Character>
{
    /// <inheritdoc/>
    public override uint FromVersion => 4;

    /// <inheritdoc/>
    public override uint ToVersion => 5;

    /// <inheritdoc/>
    protected override Character ApplyValue(Character value)
    {
        KeyedSlotEntry[]? migrated = value.KeyedInventory ?? KeyedInventoryMigration.FromLegacySlots(value.Inventory);
        return value with
        {
            Version = value.Version with { DataVersion = ToVersion },
            KeyedInventory = migrated,
            Inventory = null,
        };
    }
}
