using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Meta;

namespace WaywardBeyond.Client.Core.Saves.LoadOrNewGame;

internal sealed class CharacterLoadStage(in CharacterSaveManager characterSaveManager, in IAssetDatabase<LocalizedTags> localizedTagDatabase) : ILoadStage
{
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
        IReadOnlyList<string>? tags = localizedTags.Success ? localizedTags.Value.GetValues("character_load") : null;
        
        _status = tags != null ? _randomizer.Select(tags) : string.Empty;
        _lastStatusChangeTime = DateTime.UtcNow;
        
        return _status;
    }

    public Task Load()
    {
        _progress = 0f;
        _characterSaveManager.Load();
        _progress = 1f;
        return Task.CompletedTask;
    }
}