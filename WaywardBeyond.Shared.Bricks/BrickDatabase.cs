using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Shared.Bricks;

/// <summary>
/// Provides headless access to brick definitions from virtual resources. Owns the deterministic brick-id
/// registry for the loaded content and exposes it as the process <see cref="IBrickIdMap"/>. It carries
/// no render-coupled mesh or shape-light types, so the client, the server, and headless consumers share
/// one database.
/// </summary>
public sealed class BrickDatabase : VirtualAssetDatabase<BrickDefinitions, BrickDefinition, BrickInfo>, IAutoActivate, IBrickDatabase, IBrickIdMap
{
    private readonly Dictionary<ushort, BrickInfo> _bricksByDataID = [];

    /// <summary>
    /// The deterministic id space for this load of brick content. Owned here because this database is
    /// the single authority over what bricks are present; every other consumer resolves brick ids
    /// through the <see cref="IBrickIdMap"/> this exposes.
    /// </summary>
    private readonly BrickIdRegistry _registry;

    public BrickDatabase(
        in ILogger<BrickDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs
    ) : base(logger, fileParseService, vfs)
    {
        _registry = BuildRegistry();
        Load();
    }

    /// <inheritdoc/>
    public ushort Id(string name) => _registry.Id(name);

    /// <inheritdoc/>
    public string? Name(ushort id) => _registry.Name(id);

    /// <inheritdoc/>
    public int Count => _registry.Count;

    /// <summary>Returns whether a block-shaped voxel of the provided shape culls faces around it.</summary>
    public bool IsCuller(in Voxel voxel, BrickShape shape)
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
    protected override PathInfo GetRootPath() => new PathInfo("bricks/");

    /// <inheritdoc/>
    protected override IEnumerable<BrickDefinition> GetAssetInfo(PathInfo path, BrickDefinitions resource) => resource.Bricks;

    /// <inheritdoc/>
    protected override string GetAssetID(BrickDefinition assetInfo) => assetInfo.ID;

    /// <inheritdoc/>
    protected override Result<BrickInfo> LoadAsset(string id, BrickDefinition assetInfo)
    {
        ushort dataID = _registry.Id(id);
        var brickInfo = new BrickInfo(id, dataID, assetInfo.Transparent, assetInfo.Passable, assetInfo.Mesh, assetInfo.Shape, assetInfo.Textures, assetInfo.Tags);
        lock (_bricksByDataID)
        {
            _bricksByDataID[dataID] = brickInfo;
        }

        return Result<BrickInfo>.FromSuccess(brickInfo);
    }

    /// <summary>
    /// Collects every brick id under the brick root and builds the registry over
    /// <see cref="BaseBrickCatalog.Registry"/> plus the loaded set, so the whole id space is known
    /// before individual bricks are loaded.
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