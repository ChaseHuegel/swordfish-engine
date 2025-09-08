using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.Bricks;

/// <summary>
///     Provides access to brick information from virtual resources.
/// </summary>
internal sealed class BrickDatabase : VirtualAssetDatabase<BrickDefinitions, BrickDefinition, BrickInfo>, IAutoActivate
{
    private ushort _lastDataID;

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
        //  Allocate an incremented ID for each brick.
        //  This is not externally unique, and is valid solely for the current run of the game.
        _lastDataID++;
        
        Mesh? mesh = null;
        if (assetInfo.Shape == BrickShape.Custom && assetInfo.Mesh != null)
        {
            Result<Mesh> meshResult = _meshDatabase.Get(assetInfo.Mesh);
            if (meshResult.Success)
            {
                mesh = meshResult.Value;
            }
        }
        
        var brickInfo = new BrickInfo(id, _lastDataID, assetInfo.Transparent, assetInfo.Passable, mesh, assetInfo.Shape, assetInfo.Textures);
        lock (_bricksByDataID)
        {
            _bricksByDataID[_lastDataID] = brickInfo;
        }
        
        return Result<BrickInfo>.FromSuccess(brickInfo);
    }
}