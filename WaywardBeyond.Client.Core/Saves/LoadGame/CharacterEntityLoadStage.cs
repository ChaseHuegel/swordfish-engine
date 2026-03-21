using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Characters;
using WaywardBeyond.Client.Core.Meta;
using WaywardBeyond.Client.Core.Voxels.Building;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Saves.LoadGame;

internal sealed class CharacterEntityLoadStage(
    in ISerializer<CharacterEntityModel> characterEntitySerializer,
    in PlayerCharacterEntityBuilder playerCharacterEntityBuilder,
    in CharacterSaveManager characterSaveManager,
    in IAssetDatabase<LocalizedTags> localizedTagDatabase
) : ILoadStage<GameSave>
{
    private readonly ISerializer<CharacterEntityModel> _characterEntitySerializer = characterEntitySerializer;
    private readonly PlayerCharacterEntityBuilder _playerCharacterEntityBuilder = playerCharacterEntityBuilder;
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
        CharacterSave characterSave = _characterSaveManager.ActiveSave.Value;
        CharacterEntityModel? characterEntityModel = null;
        
        //  Find the entity save matching the character save
        PathInfo[] characterEntityFiles = save.Path.At(GameSaveService.CHARACTER_ENTITIES_SUBFOLDER).GetFiles();
        foreach (PathInfo voxelEntityFile in characterEntityFiles.OrderBy(pathInfo => pathInfo.OriginalString, new NaturalComparer()))
        {
            byte[] data = voxelEntityFile.ReadBytes();
            CharacterEntityModel deserializedCharacterEntity = _characterEntitySerializer.Deserialize(data);

            if (deserializedCharacterEntity.Guid.ToString() != characterSave.Character.Guid)
            {
                continue;
            }

            characterEntityModel = deserializedCharacterEntity;
            break;
        }
        
        if (characterEntityModel == null)
        {
            //  No entity found, create a new one
            var spawnPosition = new Vector3(save.Level.SpawnX, save.Level.SpawnY, save.Level.SpawnZ);
            Guid guid = Guid.Parse(characterSave.Character.Guid);
            characterEntityModel = new CharacterEntityModel(guid, spawnPosition, Quaternion.Identity, GameMode.Creative);
        }

        _playerCharacterEntityBuilder.Create(characterSave.Character, characterEntityModel.Value);
        _progress = 1f;
        return Task.CompletedTask;
    }
}