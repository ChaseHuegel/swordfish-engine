using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Statistics;

namespace WaywardBeyond.Client.Core.Saves;

internal sealed class CharacterSaveManager
{
    public CharacterSave? ActiveSave
    {
        get
        {
            using Lock.Scope _ = _activeSaveLock.EnterScope();
            return _activeSave;
        }
        set
        {
            using Lock.Scope _ = _activeSaveLock.EnterScope();
            _activeSave = value;
        }
    }

    private readonly ILogger<CharacterSaveManager> _logger;
    private readonly CharacterSaveService _characterSaveService;

    private readonly Lock _activeSaveLock = new();
    private CharacterSave? _activeSave;

    public CharacterSaveManager(in ILogger<CharacterSaveManager> logger, in CharacterSaveService characterSaveService)
    {
        _logger = logger;
        _characterSaveService = characterSaveService;

        //  Default to the most recent character save, if there is one
        ActiveSave = GetMostRecentSave();
    }

    public Result<CharacterSave> Load()
    {
        lock (_activeSaveLock)
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
            
            save = new CharacterSave(save.Path, character);
            ActiveSave = save;
            
            return Result<CharacterSave>.FromSuccess(save);
        }
    }

    public void Save()
    {
        if (WaywardBeyond.GameState < GameState.Playing)
        {
            return;
        }

        using Lock.Scope _ = _activeSaveLock.EnterScope();

        if (ActiveSave == null)
        {
            return;
        }

        Character character = ActiveSave.Value.Character;

        long nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        StatisticInfo lastPlayedMs = character.SetStatistic("lastPlayed.ms", nowUtcMs);
        character.AddStatistic("age.ms", nowUtcMs - lastPlayedMs.Previous);

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