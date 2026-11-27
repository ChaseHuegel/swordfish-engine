using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Localization;

namespace WaywardBeyond.Bricks;

/// <inheritdoc/>
internal sealed class BrickDatabase : VirtualAssetDatabase<BrickDefinitions, BrickDefinition, Brick>, IAutoActivate, IBrickDatabase, IBrickRegistry
{
    private readonly ILogger<BrickDatabase> _logger;
    private readonly LocalizedTags? _invariantTags;
    private readonly List<Brick> _loaded = [];
    private readonly BrickRegistry _registry;
    private readonly Brick?[] _byId;

    /// <summary>Constructs a brick database.</summary>
    public BrickDatabase(
        in ILogger<BrickDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs,
        in ILocalizedTagsDatabase tagDatabase
    ) : base(logger, fileParseService, vfs)
    {
        _logger = logger;
        _invariantTags = tagDatabase.Invariant;
        Load();

        _registry = BrickRegistry.FromNames([.. _loaded.Select(brick => brick.ID)]);
        _byId = new Brick?[_registry.Count];
        for (var i = 0; i < _loaded.Count; i++)
        {
            Brick brick = _loaded[i];
            brick.BindDataID(_registry.Id(brick.ID));
            _byId[brick.DataID - Brick.MIN_ID] = brick;
        }
    }

    /// <inheritdoc/>
    public ushort Id(string name) => _registry.Id(name);

    /// <inheritdoc/>
    public ushort Resolve(string name) => _registry.Resolve(name);

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
        return brick != null && !brick.Passable && brick.Alpha == BrickAlpha.Opaque && brick.CastShadows;
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
        BrickColliderShape[] colliders = BuildColliders(id, assetInfo.Colliders);
        bool lightSource = _invariantTags?.Contains(BrickTags.LIGHT, id) ?? false;
        bool entity = _invariantTags?.Contains(BrickTags.ENTITY, id) ?? false;
        bool paintable = _invariantTags?.Contains(BrickTags.PAINTABLE, id) ?? false;
        bool orientable = _invariantTags?.Contains(BrickTags.ORIENTABLE, id) ?? false;
        var brick = new Brick(
            id,
            assetInfo.Alpha,
            assetInfo.Passable,
            assetInfo.Mesh,
            assetInfo.Shape,
            assetInfo.Textures,
            assetInfo.CastShadows,
            colliders,
            assetInfo.Sounds,
            lightSource,
            entity,
            paintable,
            orientable,
            ResolveColor(id, assetInfo.Color),
            ResolveHardness(id, assetInfo.Hardness)
        );
        _loaded.Add(brick);
        return Result<Brick>.FromSuccess(brick);
    }

    private Vector3 ResolveColor(string id, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Vector3.One;
        }

        if (!HexColor.TryParseRgb(value, out Vector3 color))
        {
            _logger.LogWarning("Brick \"{BrickId}\" has invalid hex color \"{Color}\". Using white.", id, value);
            return Vector3.One;
        }

        return color;
    }

    private float ResolveHardness(string id, float? value)
    {
        if (!value.HasValue)
        {
            return Brick.DEFAULT_HARDNESS;
        }

        if (!float.IsFinite(value.Value) || value.Value < 0f)
        {
            _logger.LogWarning(
                "Brick \"{BrickId}\" has invalid Hardness {Hardness}. Using {Default}.",
                id,
                value.Value,
                Brick.DEFAULT_HARDNESS);
            return Brick.DEFAULT_HARDNESS;
        }

        return value.Value;
    }

    private BrickColliderShape[] BuildColliders(string brickId, List<BrickColliderDefinition>? definitions)
    {
        if (definitions == null || definitions.Count == 0)
        {
            return [];
        }

        var built = new List<BrickColliderShape>(definitions.Count);
        for (var i = 0; i < definitions.Count; i++)
        {
            Result<BrickColliderShape> result = definitions[i].Build();
            if (result.Success)
            {
                built.Add(result.Value);
            }
            else
            {
                _logger.LogWarning("Skipping invalid collider #{Index} on brick \"{BrickId}\": {Error}", i, brickId, result.Message);
            }
        }

        return built.Count > 0 ? [.. built] : [];
    }

    private Brick? GetOrNull(ushort id)
    {
        if (id < 1)
        {
            return null;
        }

        int index = id - Brick.MIN_ID;
        return index < _byId.Length ? _byId[index] : null;
    }
}