using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.Bricks;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Serialization;

namespace WaywardBeyond.Client.Core.Bricks;

/// <summary>
///     Provides access to brick information from virtual resources.
/// </summary>
internal sealed class BrickDatabase : VirtualAssetDatabase<BrickDefinitions, BrickDefinition, BrickInfo>, IAutoActivate
{
    private readonly IAssetDatabase<Mesh> _meshDatabase;
    private readonly Dictionary<ushort, BrickInfo> _bricksByDataID = [];
    
    public BrickDatabase(
        in ILogger<BrickDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs,
        in IAssetDatabase<Mesh> meshDatabase)
        : base(logger, fileParseService, vfs)
    {
        _meshDatabase = meshDatabase;
        Load();
    }

    public bool IsCuller(Brick brick)
    {
        if (brick.ID == 0)
        {
            return false;
        }
        
        //  If this is not a block shape, it doesn't cull
        BrickData data = brick.Data;
        if (data.Shape != BrickShape.Block)
        {
            return false;
        }
        
        BrickInfo brickInfo = Get(brick.ID).Value;
        return !brickInfo.Passable && !brickInfo.Transparent;
    }
    
    /// <summary>
    ///     Attempts to get a brick's info by its data ID.
    /// </summary>
    public Result<BrickInfo> Get(ushort id)
    {
        lock (_bricksByDataID)
        {
            if (_bricksByDataID.TryGetValue(id, out BrickInfo? value))
            {
                return Result<BrickInfo>.FromSuccess(value);
            }
            
            return Result<BrickInfo>.FromFailure($"Unknown brick \"{id}\"");
        }
    }

    /// <summary>
    ///     Attempts to get all brick's infos that match a predicate.
    /// </summary>
    public List<BrickInfo> Get(Func<BrickInfo, bool> predicate)
    {
        lock (_bricksByDataID)
        {
            return _bricksByDataID.Values.Where(predicate).ToList();
        }
    }
    
    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".toml");
    
    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => AssetPaths.Root.At("bricks");
    
    /// <inheritdoc/>
    protected override IEnumerable<BrickDefinition> GetAssetInfo(PathInfo path, BrickDefinitions resource) => resource.Bricks;

    /// <inheritdoc/>
    protected override string GetAssetID(BrickDefinition assetInfo) => assetInfo.ID;
    
    /// <inheritdoc/>
    protected override Result<BrickInfo> LoadAsset(string id, BrickDefinition assetInfo)
    {
        Result<ushort> dataIDResult = GenerateDataID(id);
        if (!dataIDResult.Success)
        {
            return new Result<BrickInfo>(success: false, null!, dataIDResult.Message, dataIDResult.Exception);
        }
        
        Mesh? mesh = null;
        if (assetInfo.Shape == BrickShape.Custom && assetInfo.Mesh != null)
        {
            Result<Mesh> meshResult = _meshDatabase.Get(assetInfo.Mesh);
            if (meshResult.Success)
            {
                mesh = meshResult.Value;
            }
        }
        
        var brickInfo = new BrickInfo(id, dataIDResult, assetInfo.Transparent, assetInfo.Passable, mesh, assetInfo.Shape, assetInfo.Textures, assetInfo.Tags);
        lock (_bricksByDataID)
        {
            _bricksByDataID[dataIDResult] = brickInfo;
        }
        
        return Result<BrickInfo>.FromSuccess(brickInfo);
    }

    private Result<ushort> GenerateDataID(string str)
    {
        uint hash = FNV1a.ComputeHash32(str);
        var id = (ushort)(hash % ushort.MaxValue);
        
        var collisions = 0;
        lock (_bricksByDataID)
        {
            ushort startID = id;
            while (_bricksByDataID.ContainsKey(id))
            {
                collisions++;
                id++;
                
                if (id == ushort.MaxValue)
                {
                    id = 0;
                }
                else if (id == startID)
                {
                    return Result<ushort>.FromFailure("No brick IDs are available");
                }
            }
        }
        
        if (collisions != 0)
        {
            Logger.LogWarning("Brick \"{str}\" had {collisions} ID collisions! This brick may not be stable across load orders.", str, collisions);
        }
        
        return Result<ushort>.FromSuccess(id);
    }
}