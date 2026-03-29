using System;
using System.Linq;
using System.Threading;
using Swordfish.Library.Util;

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
    
    private readonly CharacterSaveService _characterSaveService;

    private readonly Lock _activeSaveLock = new();
    private CharacterSave? _activeSave;

    public CharacterSaveManager(in CharacterSaveService characterSaveService)
    {
        _characterSaveService = characterSaveService;

        CharacterSave mostRecentSave = characterSaveService.GetSaves()
            .OrderByDescending(save => save.Character.LastPlayedMs)
            .FirstOrDefault();
        
        //  Default to the most recent character save, if there is one
        ActiveSave = mostRecentSave.Path.Exists() ? mostRecentSave : null;
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
}