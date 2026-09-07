using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Meta;
using WaywardBeyond.Client.Core.Systems;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Client.Core.Saves.LoadGame;

internal sealed class CharacterEntityLoadStage(
    in ClientPlayerSpawnSystem clientPlayerSpawnSystem,
    in CharacterSaveManager characterSaveManager,
    in IAssetDatabase<LocalizedTags> localizedTagDatabase
) : ILoadStage<GameSave>
{
    private readonly ClientPlayerSpawnSystem _clientPlayerSpawnSystem = clientPlayerSpawnSystem;
    private readonly CharacterSaveManager _characterSaveManager = characterSaveManager;
    private readonly IAssetDatabase<LocalizedTags> _localizedTagDatabase = localizedTagDatabase;
    private readonly Randomizer _randomizer = new();

    private float _progress;
    private string _status = string.Empty;
    private DateTime _lastStatusChangeTime;
    
    public float GetProgress()
    {
        return _progress;
    }

    public string GetStatus()
    {
        if ((DateTime.UtcNow - _lastStatusChangeTime).TotalSeconds < 3)
        {
            return _status;
        }
        
        Result<LocalizedTags> localizedTags = _localizedTagDatabase.Get(CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
        IReadOnlyList<string>? tags = localizedTags.Success ? localizedTags.Value.GetValues("game_load") : null;
        
        _status = tags != null ? _randomizer.Select(tags) : string.Empty;
        _lastStatusChangeTime = DateTime.UtcNow;
        
        return _status;
    }
    
    public Task Load(GameSave save)
    {
        if (_characterSaveManager.ActiveSave == null)
        {
            _progress = 1f;
            return Task.CompletedTask;
        }

        _progress = 0f;
        Character character = _characterSaveManager.ActiveSave.Value;

        //  The server owns the initial transform; it assigns the spawn and replicates it downstream.
        _clientPlayerSpawnSystem.RequestSpawn(character);
        _progress = 1f;
        return Task.CompletedTask;
    }
}