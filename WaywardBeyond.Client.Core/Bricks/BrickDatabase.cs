using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Client.Core.Voxels.Models;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Client.Core.Bricks;

/// <summary>
///     Provides access to brick information from virtual resources.
/// </summary>
internal sealed class BrickDatabase : VirtualAssetDatabase<BrickDefinitions, BrickDefinition, BrickInfo>, IAutoActivate, IBrickDatabase
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
    
    public bool IsCuller(Voxel voxel)
    {
        return IsCuller(voxel, voxel.GetShapeLight().Shape);
    }
    
    public bool IsCuller(Voxel voxel, ShapeLight shapeLight)
    {
        return IsCuller(voxel, shapeLight.Shape);
    }
    
    public bool IsCuller(Voxel voxel, BrickShape shape)
    {
        if (voxel.ID == 0)
        {
            return false;
        }
        
        //  If this is not a block shape, it doesn't cull
        if (shape != BrickShape.Block)
        {
            return false;
        }
        
        BrickInfo brickInfo = Get(voxel.ID).Value;
        return !brickInfo.Passable && !brickInfo.Transparent;
    }
    
    /// <inheritdoc/>
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

    
    /// <inheritdoc/>
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
        //  The id is a pure, deterministic hash of the brick name so it is identical on the client,
        //  the server, worldgen, and skills regardless of asset load order. This id must never be
        //  shifted by insertion order: a genuine FNV collision between two brick names is a hard error
        //  rather than a silent remap, so it cannot corrupt saves or diverge across runs.
        ushort id = FNV1a.ComputeDataID(str);

        lock (_bricksByDataID)
        {
            if (_bricksByDataID.TryGetValue(id, out BrickInfo? existing) && existing.ID != str)
            {
                return Result<ushort>.FromFailure(
                    $"Brick \"{str}\" collides with \"{existing.ID}\": both hash to voxel id {id}. Rename one of them.");
            }
        }

        return Result<ushort>.FromSuccess(id);
    }
}