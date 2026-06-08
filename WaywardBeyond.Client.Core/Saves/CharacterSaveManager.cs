using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using NATS.Client.KeyValueStore;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Saves.Migrations;
using WaywardBeyond.Server.Core.Streaming;

namespace WaywardBeyond.Client.Core.Saves;

internal sealed class CharacterSaveManager
{
    private const string CHARACTERS_BUCKET = "characters";
    private const string CHARACTER_DATA_BUCKET = "characterData";
    
    public CharacterSave? ActiveSave
    {
        get => _activeCharacterSave.ActiveSave;
        set => _activeCharacterSave.ActiveSave = value;
    }

    private readonly ILogger<CharacterSaveManager> _logger;
    private readonly CharacterSaveService _characterSaveService;
    private readonly ActiveCharacterSave _activeCharacterSave;
    private readonly ICharacterMigration[] _characterMigrations;
    private readonly KeyValueStore _kvStore;

    public CharacterSaveManager(
        in ILogger<CharacterSaveManager> logger,
        in CharacterSaveService characterSaveService, 
        in ActiveCharacterSave activeCharacterSave,
        in ICharacterMigration[] characterMigrations,
        in KeyValueStore kvStore
    ) {
        _logger = logger;
        _characterSaveService = characterSaveService;
        _activeCharacterSave = activeCharacterSave;
        _characterMigrations = characterMigrations;
        _kvStore = kvStore;

        //  Default to the most recent character save, if there is one
        ActiveSave = GetMostRecentSave();
    }

    public Result<CharacterSave> Load()
    {
        if (ActiveSave == null)
        {
            return Result<CharacterSave>.FromFailure("No character selected");
        }
        
        CharacterSave save = ActiveSave.Value;
        
        long nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Character character = save.Character with
        {
            LastPlayedMs = nowUtcMs,
        };

        //  Process any migration
        for (var i = 0; i < _characterMigrations.Length; i++)
        {
            _characterMigrations[i].Process(ref character);
        }

        //  Version up the character
        character.Version = WaywardBeyond.Version;
        
        save = new CharacterSave(character);
        ActiveSave = save;
        
        return Result<CharacterSave>.FromSuccess(save);
    }

    public void Save()
    {
        if (WaywardBeyond.GameState < GameState.Playing)
        {
            return;
        }

        if (ActiveSave == null)
        {
            return;
        }

        long nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Character character = ActiveSave.Value.Character with
        {
            AgeMs = ActiveSave.Value.Character.AgeMs + nowUtcMs - ActiveSave.Value.Character.LastPlayedMs,
            LastPlayedMs = nowUtcMs,
        };

        var save = new CharacterSave(character);
        Result<CharacterSave> saveResult = _characterSaveService.Save(save);

        if (saveResult.Success)
        {
            ActiveSave = saveResult.Value;
        }
    }
    
    public void Delete(CharacterSave save)
    {
        try
        {
            Result<NatsKVEntry<byte[]>[]> getResult = _kvStore.GetAll<byte[]>(CHARACTERS_BUCKET);
            if (!getResult.Success) return;

            string[] keysToDelete = getResult.Value
                .Where(entry => entry.Key.StartsWith(save.Character.Guid))
                .Select(entry => entry.Key)
                .ToArray();

            _kvStore.Delete(CHARACTERS_BUCKET, keysToDelete);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error trying to delete character \"{character}\" ({guid})", save.Character.Name, save.Character.Guid);
        }
    }
    
    internal CharacterSave? GetMostRecentSave()
    {
        CharacterSave? mostRecentSave = _characterSaveService.GetSaves()
            .OrderByDescending(save => save.Character.LastPlayedMs)
            .FirstOrDefault();
        
        return mostRecentSave;
    }
}
