using System;
using System.Threading;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.Saves;

internal sealed class CharacterSaveManager(in CharacterSaveService characterSaveService) 
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
    
    private readonly CharacterSaveService _characterSaveService = characterSaveService;

    private readonly Lock _activeSaveLock = new();
    private CharacterSave? _activeSave;
    
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

        CharacterSave save = ActiveSave.Value;
        
        long nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Character character = save.Character with
        {
            AgeMs = nowUtcMs - save.Character.LastPlayedMs,
        };
        
        save = new CharacterSave(save.Path, character);
        _characterSaveService.Save(save);
    }
}