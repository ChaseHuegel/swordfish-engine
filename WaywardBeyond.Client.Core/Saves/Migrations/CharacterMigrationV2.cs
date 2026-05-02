namespace WaywardBeyond.Client.Core.Saves.Migrations;

internal class CharacterMigrationV2 : ICharacterMigration
{
    public void Process(ref Character character)
    {
        if (character.Version.DataVersion >= 2)
        {
            return;
        }
        
        //  v2 change statistics values from int->long
        //      Reset statistics, previous values are unusable
        character.Statistics = [];
    }
}