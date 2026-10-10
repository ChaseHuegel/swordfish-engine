using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Data;

namespace WaywardBeyond.Bricks;

/// <inheritdoc/>
internal sealed class BrickDatabase : VirtualAssetDatabase<BrickDefinitions, BrickDefinition, Brick>, IAutoActivate, IBrickDatabase, IBrickRegistry
{
    private readonly List<Brick> _loaded = [];
    private readonly BrickRegistry _registry;
    private readonly Brick?[] _byId;

    /// <summary>Constructs a brick database.</summary>
    public BrickDatabase(
        in ILogger<BrickDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs
    ) : base(logger, fileParseService, vfs)
    {
        Load();

        _registry = BrickRegistry.FromNames([.. _loaded.Select(brick => brick.ID)]);
        _byId = new Brick?[_registry.Count];
        for (var i = 0; i < _loaded.Count; i++)
        {
            Brick brick = _loaded[i];
            brick.BindDataID(_registry.Id(brick.ID));
            _byId[brick.DataID - Brick.MinID] = brick;
        }
    }

    /// <inheritdoc/>
    public ushort Id(string name) => _registry.Id(name);

    /// <inheritdoc/>
    public string? Name(ushort id) => _registry.Name(id);

    /// <inheritdoc/>
    public bool IsCuller(in Voxel voxel, BrickShape shape)
    {
        if (voxel.ID == 0 || shape != BrickShape.Block)
        {
            return false;
        }

        Brick? brick = GetOrNull(voxel.ID);
        return brick != null && !brick.Passable && !brick.Transparent;
    }

    /// <inheritdoc/>
    public Result<Brick> Get(ushort id)
    {
        Brick? brick = GetOrNull(id);
        return brick != null ? Result<Brick>.FromSuccess(brick) : Result<Brick>.FromFailure($"Unknown brick \"{id}\"");
    }

    /// <inheritdoc/>
    public List<Brick> Get(Func<Brick, bool> predicate)
    {
        return [.. _byId.OfType<Brick>().Where(predicate)];
    }

    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".toml");

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => new("bricks/");

    /// <inheritdoc/>
    protected override IEnumerable<BrickDefinition> GetAssetInfo(PathInfo path, BrickDefinitions resource) => resource.Bricks;

    /// <inheritdoc/>
    protected override string GetAssetID(BrickDefinition assetInfo) => assetInfo.ID;

    /// <inheritdoc/>
    protected override Result<Brick> LoadAsset(string id, BrickDefinition assetInfo)
    {
        var brick = new Brick(id, assetInfo.Transparent, assetInfo.Passable, assetInfo.Mesh, assetInfo.Shape, assetInfo.Textures, assetInfo.Tags);
        _loaded.Add(brick);
        return Result<Brick>.FromSuccess(brick);
    }

    private Brick? GetOrNull(ushort id)
    {
        if (id < 1)
        {
            return null;
        }

        int index = id - Brick.MinID;
        return index < _byId.Length ? _byId[index] : null;
    }
}