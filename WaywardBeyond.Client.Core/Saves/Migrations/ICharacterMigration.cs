namespace WaywardBeyond.Client.Core.Saves.Migrations;

internal interface ICharacterMigration
{
    void Process(ref Character character);
}