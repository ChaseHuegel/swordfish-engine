using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Saves.Migrations;

namespace WaywardBeyond.Client.Core.Saves;

internal sealed class CharacterSaveManager
{
    public CharacterSave? ActiveSave
    {
        get => _activeCharacterSave.ActiveSave;
        set => _activeCharacterSave.ActiveSave = value;
    }

    private readonly ILogger<CharacterSaveManager> _logger;
    private readonly CharacterSaveService _characterSaveService;
    private readonly ActiveCharacterSave _activeCharacterSave;
    private readonly ICharacterMigration[] _characterMigrations;

    public CharacterSaveManager(
        in ILogger<CharacterSaveManager> logger,
        in CharacterSaveService characterSaveService, 
        in ActiveCharacterSave activeCharacterSave,
        in ICharacterMigration[] characterMigrations
    ) {
        _logger = logger;
        _characterSaveService = characterSaveService;
        _activeCharacterSave = activeCharacterSave;
        _characterMigrations = characterMigrations;

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
        
        save = new CharacterSave(save.Path, character);
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

        var save = new CharacterSave(ActiveSave.Value.Path, character);
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
            File.Delete(save.Path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error trying to delete character \"{character}\" ({guid})", save.Character.Name, save.Character.Guid);
        }
    }
    
    internal CharacterSave? GetMostRecentSave()
    {
        CharacterSave mostRecentSave = _characterSaveService.GetSaves()
            .OrderByDescending(save => save.Character.LastPlayedMs)
            .FirstOrDefault();
        
        return mostRecentSave.Path.FileExists() ? mostRecentSave : null;
    }
}