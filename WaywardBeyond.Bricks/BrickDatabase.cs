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
        bool paintable = _invariantTags?.Contains(BrickTags.PAINTABLE, id) ?? false;
        bool orientable = _invariantTags?.Contains(BrickTags.ORIENTABLE, id) ?? false;
        bool airtight = _invariantTags?.Contains(BrickTags.AIRTIGHT, id) ?? false;
        bool toggleable = _invariantTags?.Contains(BrickTags.TOGGLEABLE, id) ?? false;
        bool defaultPowerEnabled = !(_invariantTags?.Contains(BrickTags.DEFAULT_POWER_OFF, id) ?? false);
        bool defaultOpen = _invariantTags?.Contains(BrickTags.DEFAULT_OPEN, id) ?? false;
        string stationTag = ResolveStationTag(id);
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
            paintable,
            orientable,
            ResolveColor(id, assetInfo.Color),
            ResolveStatistics(id, assetInfo.Statistics),
            ResolveEntity(id, assetInfo.Entity),
            airtight,
            toggleable,
            defaultPowerEnabled,
            defaultOpen,
            stationTag
        );
        _loaded.Add(brick);
        return Result<Brick>.FromSuccess(brick);
    }

    private string ResolveStationTag(string brickId)
    {
        if (_invariantTags == null)
        {
            return string.Empty;
        }

        string? matched = null;
        foreach (string key in _invariantTags.Keys)
        {
            if (!key.StartsWith(BrickTags.STATION_PREFIX, StringComparison.Ordinal)
                || !_invariantTags.Contains(key, brickId))
            {
                continue;
            }

            if (matched == null || string.CompareOrdinal(key, matched) < 0)
            {
                matched = key;
            }
        }

        return matched ?? string.Empty;
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

    private IReadOnlyDictionary<string, float>? ResolveStatistics(string id, Dictionary<string, float>? statistics)
    {
        if (statistics == null || statistics.Count == 0)
        {
            return null;
        }

        Dictionary<string, float>? resolved = null;
        foreach (KeyValuePair<string, float> entry in statistics)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                _logger.LogWarning("Brick \"{BrickId}\" declares a statistic with an empty key. Skipping.", id);
                continue;
            }

            if (!float.IsFinite(entry.Value))
            {
                _logger.LogWarning(
                    "Brick \"{BrickId}\" statistic \"{StatisticId}\" has non-finite value {Value}. Skipping.",
                    id,
                    entry.Key,
                    entry.Value);
                continue;
            }

            resolved ??= new Dictionary<string, float>(statistics.Count, StringComparer.Ordinal);
            resolved[entry.Key] = entry.Value;
        }

        return resolved;
    }

    private BrickEntity? ResolveEntity(string id, BrickEntityDefinition? definition)
    {
        if (definition == null)
        {
            return null;
        }

        BrickEntityDefinition def = definition.Value;
        BrickLight? light = ResolveLight(id, def.Light);
        BrickSlotGroup[] slotGroups = ResolveSlotGroups(id, def.SlotGroups);
        return new BrickEntity(light, slotGroups);
    }

    private BrickLight? ResolveLight(string id, BrickLightDefinition? definition)
    {
        if (definition == null)
        {
            return null;
        }

        BrickLightDefinition def = definition.Value;
        float? radius = def.Radius;
        if (radius.HasValue && (!float.IsFinite(radius.Value) || radius.Value < 0f))
        {
            _logger.LogWarning("Brick \"{BrickId}\" has invalid Entity.Light.Radius {Value}. Ignoring.", id, radius.Value);
            radius = null;
        }

        float size = def.Size;
        if (!float.IsFinite(size) || size <= 0f)
        {
            _logger.LogWarning("Brick \"{BrickId}\" has invalid Entity.Light.Size {Value}. Using 2.4.", id, size);
            size = 2.4f;
        }

        float intensity = def.Intensity;
        if (!float.IsFinite(intensity) || intensity < 0f)
        {
            _logger.LogWarning("Brick \"{BrickId}\" has invalid Entity.Light.Intensity {Value}. Using 1.0.", id, intensity);
            intensity = 1.0f;
        }

        Vector3 color = ResolveColor(id, def.Color);
        Vector3 offset = Vector3.Zero;
        if (!def.Offset.IsFinite)
        {
            _logger.LogWarning("Brick \"{BrickId}\" has non-finite Entity.Light.Offset. Using zero.", id);
        }
        else
        {
            offset = def.Offset.ToVector3();
        }

        return new BrickLight(radius, size, color, intensity, def.CastShadows, offset, def.TintFromPaint);
    }

    private BrickSlotGroup[] ResolveSlotGroups(string brickId, List<BrickSlotGroupDefinition>? definitions)
    {
        if (definitions == null || definitions.Count == 0)
        {
            return [];
        }

        var resolved = new List<BrickSlotGroup>(definitions.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < definitions.Count; i++)
        {
            BrickSlotGroupDefinition entry = definitions[i];
            string groupId = string.IsNullOrWhiteSpace(entry.ID) ? string.Empty : entry.ID.Trim();
            if (groupId.Length == 0)
            {
                _logger.LogWarning("Brick \"{BrickId}\" declares an Entity slot group with an empty ID. Skipping.", brickId);
                continue;
            }

            if (entry.Capacity.HasValue && entry.Capacity.Value < 0)
            {
                _logger.LogWarning(
                    "Brick \"{BrickId}\" slot group \"{GroupId}\" has negative capacity {Capacity}. Skipping.",
                    brickId,
                    groupId,
                    entry.Capacity.Value);
                continue;
            }

            if (!seen.Add(groupId))
            {
                _logger.LogWarning("Brick \"{BrickId}\" declares duplicate Entity slot group \"{GroupId}\". Skipping.", brickId, groupId);
                continue;
            }

            resolved.Add(new BrickSlotGroup(groupId, entry.Capacity));
        }

        return resolved.Count > 0 ? [.. resolved] : [];
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
