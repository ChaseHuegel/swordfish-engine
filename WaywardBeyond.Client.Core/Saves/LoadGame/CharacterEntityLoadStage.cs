using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Swordfish.Library.Collections;
using Swordfish.Library.Serialization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Meta;
using WaywardBeyond.Client.Core.Voxels.Building;
using WaywardBeyond.Client.Core.Voxels.Models;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Client.Core.Saves.LoadGame;

internal sealed class CharacterEntityLoadStage(
    in ISerializer<CharacterEntityModel> characterEntitySerializer,
    in PlayerCharacterEntityBuilder playerCharacterEntityBuilder,
    in CharacterSaveManager characterSaveManager,
    in IAssetDatabase<LocalizedTags> localizedTagDatabase,
    in KeyValueStore keyValueStore
) : ILoadStage<GameSave>
{
    private readonly ISerializer<CharacterEntityModel> _characterEntitySerializer = characterEntitySerializer;
    private readonly PlayerCharacterEntityBuilder _playerCharacterEntityBuilder = playerCharacterEntityBuilder;
    private readonly CharacterSaveManager _characterSaveManager = characterSaveManager;
    private readonly IAssetDatabase<LocalizedTags> _localizedTagDatabase = localizedTagDatabase;
    private readonly KeyValueStore _keyValueStore = keyValueStore;
    private readonly Randomizer _randomizer = new();

    private const string BUCKET_NAME = "levels";

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
        CharacterEntityModel? characterEntityModel = null;
        
        var characterKey = $"{save.Level.Guid}.characters.{character.Guid}";
        Result<byte[]> getResult = _keyValueStore.Get<byte[]>(BUCKET_NAME, characterKey);
        if (getResult.Success && getResult.Value.Length > 0)
        {
            characterEntityModel = _characterEntitySerializer.Deserialize(getResult.Value);
        }
        
        if (characterEntityModel == null)
        {
            //  No entity found, create a new one
            var spawnPosition = new Vector3(save.Level.SpawnX, save.Level.SpawnY, save.Level.SpawnZ);
            Guid guid = Guid.Parse(character.Guid);
            characterEntityModel = new CharacterEntityModel(guid, spawnPosition, Quaternion.Identity, save.Level.DefaultGameMode);
        }

        _playerCharacterEntityBuilder.Create(character, characterEntityModel.Value);
        _progress = 1f;
        return Task.CompletedTask;
    }
}
