using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

internal sealed class VoxelEntityLoadStage(
    in ISerializer<VoxelEntityModel> voxelEntitySerializer,
    in VoxelEntityBuilder voxelEntityBuilder,
    in IAssetDatabase<LocalizedTags> localizedTagDatabase,
    in KeyValueStore keyValueStore
) : ILoadStage<GameSave>
{
    private readonly ISerializer<VoxelEntityModel> _voxelEntitySerializer = voxelEntitySerializer;
    private readonly VoxelEntityBuilder _voxelEntityBuilder = voxelEntityBuilder;
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
        _progress = 0f;
        
        Result<string[]> keysResult = _keyValueStore.GetKeys(BUCKET_NAME);
        if (!keysResult.Success)
        {
            return Task.CompletedTask;
        }

        string prefix = $"{save.Level.Guid}.entity.";
        var entityKeys = new List<string>();
        for (var i = 0; i < keysResult.Value.Length; i++)
        {
            string key = keysResult.Value[i];
            if (key.StartsWith(prefix))
            {
                entityKeys.Add(key);
            }
        }
        
        var processedFiles = 0;
        foreach (string key in entityKeys.OrderBy(k => k, new NaturalComparer()))
        {
            Result<byte[]> getResult = _keyValueStore.Get<byte[]>(BUCKET_NAME, key);
            if (getResult.Success && getResult.Value.Length > 0)
            {
                VoxelEntityModel voxelEntityModel = _voxelEntitySerializer.Deserialize(getResult.Value);
                _voxelEntityBuilder.Create(voxelEntityModel.Uuid, voxelEntityModel.VoxelObject, voxelEntityModel.Position, voxelEntityModel.Orientation, Vector3.One);
            }
            
            processedFiles++;
            _progress = 1f / entityKeys.Count * processedFiles;
        }
        
        return Task.CompletedTask;
    }
}
