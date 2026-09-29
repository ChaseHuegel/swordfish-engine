using System;
using System.Collections.Generic;
using System.IO;
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
internal sealed class BrickDatabase : VirtualAssetDatabase<BrickDefinitions, BrickDefinition, BrickInfo>, IAutoActivate, IBrickDatabase, IBrickIdMap
{
    private readonly IAssetDatabase<Mesh> _meshDatabase;
    private readonly Dictionary<ushort, BrickInfo> _bricksByDataID = [];

    /// <summary>
    ///     The deterministic id space for this load of brick content. Owned here because this database is
    ///     the single authority over what bricks are present; every other consumer resolves brick ids
    ///     through the <see cref="IBrickIdMap"/> this exposes.
    /// </summary>
    private readonly BrickIdRegistry _registry;

    public BrickDatabase(
        in ILogger<BrickDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs,
        in IAssetDatabase<Mesh> meshDatabase)
        : base(logger, fileParseService, vfs)
    {
        _meshDatabase = meshDatabase;
        _registry = BuildRegistry();
        Load();
    }

    /// <inheritdoc/>
    public ushort Id(string name) => _registry.Id(name);

    /// <inheritdoc/>
    public string? Name(ushort id) => _registry.Name(id);

    /// <inheritdoc/>
    public int Count => _registry.Count;
    
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
        //  The id comes from the deterministic, collision-free registry built over every loaded brick
        //  name. It is identical on the client, the server, worldgen, and skills because the base
        //  registry assigns base bricks their stable sorted id and extra (mod) bricks append after them.
        return Result<ushort>.FromSuccess(_registry.Id(str));
    }

    /// <summary>
    ///     Collects every brick id under the brick root and builds the registry over
    ///     <see cref="BaseBrickCatalog.Registry"/> plus the loaded set, so the whole id space is known
    ///     before individual bricks are loaded.
    /// </summary>
    private BrickIdRegistry BuildRegistry()
    {
        var ids = new List<string>();
        foreach (PathInfo file in VFS.GetFiles(GetRootPath(), SearchOption.AllDirectories))
        {
            if (!IsValidFile(file))
            {
                continue;
            }

            BrickDefinitions resource = FileParseService.Parse<BrickDefinitions>(file);
            foreach (BrickDefinition definition in GetAssetInfo(file, resource))
            {
                ids.Add(definition.ID);
            }
        }

        return BrickIdRegistry.FromBaseAndExtras(BaseBrickCatalog.Registry, ids);
    }
}