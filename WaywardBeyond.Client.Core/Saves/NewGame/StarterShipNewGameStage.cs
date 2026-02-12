using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Meta;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Client.Core.Voxels.Building;

namespace WaywardBeyond.Client.Core.Saves.NewGame;

internal sealed class StarterShipNewGameStage(
    in VoxelEntityBuilder voxelEntityBuilder,
    in BrickDatabase brickDatabase,
    in IAssetDatabase<LocalizedTags> localizedTagDatabase
) : ILoadStage<GameOptions>
{
    private readonly VoxelEntityBuilder _voxelEntityBuilder = voxelEntityBuilder;
    private readonly BrickDatabase _brickDatabase = brickDatabase;
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
        IReadOnlyList<string>? tags = localizedTags.Success ? localizedTags.Value.GetValues("game_create") : null;
        
        _status = tags != null ? _randomizer.Select(tags) : string.Empty;
        _lastStatusChangeTime = DateTime.UtcNow;
        
        return _status;
    }
    
    public Task Load(GameOptions options)
    {
        _progress = 0f;
        
        var shipVoxelObject = new VoxelObject(chunkSize: 16);
        shipVoxelObject.Set(0, 0, 0, _brickDatabase.Get("core").Value.ToVoxel());
        _voxelEntityBuilder.Create(Guid.NewGuid(), shipVoxelObject, Vector3.Zero, Quaternion.Identity, Vector3.One);
        
        _progress = 1f;
        return Task.CompletedTask;
    }
}