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
using WaywardBeyond.Client.Core.Meta;
using WaywardBeyond.Client.Core.Voxels.Building;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Saves.LoadGame;

internal sealed class VoxelEntityLoadStage(
    in ISerializer<VoxelEntityModel> voxelEntitySerializer,
    in VoxelEntityBuilder voxelEntityBuilder,
    in IAssetDatabase<LocalizedTags> localizedTagDatabase
) : ILoadStage<GameSave>
{
    private const string VOXEL_ENTITIES_FOLDER = "voxelEntities/";
    
    private readonly ISerializer<VoxelEntityModel> _voxelEntitySerializer = voxelEntitySerializer;
    private readonly VoxelEntityBuilder _voxelEntityBuilder = voxelEntityBuilder;
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
        _progress = 0f;
        PathInfo[] voxelEntityFiles = save.Path.At(VOXEL_ENTITIES_FOLDER).GetFiles();
        
        var processedFiles = 0;
        foreach (PathInfo voxelEntityFile in voxelEntityFiles.OrderBy(pathInfo => pathInfo.OriginalString, new NaturalComparer()))
        {
            byte[] data = voxelEntityFile.ReadBytes();
            VoxelEntityModel voxelEntityModel = _voxelEntitySerializer.Deserialize(data);
            
            _voxelEntityBuilder.Create(voxelEntityModel.Guid, voxelEntityModel.VoxelObject, voxelEntityModel.Position, voxelEntityModel.Orientation, Vector3.One);
            
            processedFiles++;
            _progress = 1f / voxelEntityFiles.Length * processedFiles;
        }
        
        return Task.CompletedTask;
    }
}