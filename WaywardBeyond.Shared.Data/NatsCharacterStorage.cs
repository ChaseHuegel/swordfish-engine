using System.Collections.Generic;
using Swordfish.Library.Util;

namespace WaywardBeyond.Shared.Data;

public class NatsCharacterStorage : ICharacterStorage
{
    private const string BUCKET_NAME = "characters";

    private readonly KeyValueStore _keyValueStore;

    /// <summary>
    /// Migrates character records on load. Carries no character migration: the v4 Body change (int to
    /// string asset ID) is not forward-migrated, so older records pass through and an unknown body ID
    /// falls back to the first loaded body at resolution. The version gate still refuses records stamped
    /// by a newer build. Injectable for hosts that register a full migrator.
    /// </summary>
    private readonly SaveMigrator _migrator;

    public NatsCharacterStorage(in KeyValueStore keyValueStore, SaveMigrator? migrator = null)
    {
        _keyValueStore = keyValueStore;
        _migrator = migrator ?? CharacterSaveMigrations.Create();
    }

    public Result<Character> GetCharacter(ulong id)
    {
        Result<byte[]> getResult = _keyValueStore.Get<byte[]>(BUCKET_NAME, id.ToString());

        if (!getResult.Success)
        {
            return Result<Character>.FromFailure(getResult.Message);
        }

        if (getResult.Value.Length == 0)
        {
            return Result<Character>.FromFailure("Character not found (empty value).");
        }

        try
        {
            Character character = Character.Deserialize(getResult.Value);
            if (!_migrator.IsSupported(character.Version.DataVersion))
            {
                return Result<Character>.FromFailure(
                    $"Character {character.Id} uses data version {character.Version.DataVersion}, which is newer than the supported format version {SaveVersion.CurrentDataVersion}.");
            }

            return Result<Character>.FromSuccess(_migrator.Migrate(character, character.Version.DataVersion));
        }
        catch (SaveDataNotSupportedException ex)
        {
            return Result<Character>.FromFailure($"Failed to load character: {ex.Message}");
        }
        catch (System.Exception ex)
        {
            return Result<Character>.FromFailure($"Failed to deserialize character: {ex.Message}");
        }
    }

    public IEnumerable<Character> GetAllCharacters()
    {
        Result<string[]> keysResult = _keyValueStore.GetKeys(BUCKET_NAME);
        if (!keysResult.Success)
        {
            return [];
        }

        var characters = new List<Character>();
        for (var i = 0; i < keysResult.Value.Length; i++)
        {
            string key = keysResult.Value[i];
            if (!ulong.TryParse(key, out ulong id))
            {
                continue;
            }

            Result<Character> charResult = GetCharacter(id);
            if (charResult.Success)
            {
                characters.Add(charResult.Value);
            }
        }

        return characters;
    }

    public Result SaveCharacter(Character character)
    {
        try
        {
            byte[] data = character.Serialize();
            return _keyValueStore.Put(BUCKET_NAME, character.Id.ToString(), data);
        }
        catch (System.Exception ex)
        {
            return Result.FromFailure($"Failed to serialize character for saving: {ex.Message}");
        }
    }

    public Result DeleteCharacter(ulong id)
    {
        return _keyValueStore.Delete(BUCKET_NAME, id.ToString());
    }
}
