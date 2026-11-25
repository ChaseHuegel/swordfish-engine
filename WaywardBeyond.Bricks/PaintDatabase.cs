using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace WaywardBeyond.Bricks;

/// <inheritdoc/>
internal sealed class PaintDatabase : VirtualAssetDatabase<PaintDefinitions, PaintDefinition, Paint>, IAutoActivate, IPaintDatabase, IPaintRegistry
{
    private readonly ILogger<PaintDatabase> _logger;
    private readonly List<Paint> _loaded = [];
    private readonly PaintRegistry _registry;
    private readonly Paint?[] _byId;
    private readonly Paint[] _texturePaints;
    private readonly Paint[] _colorPaints;

    /// <summary>Constructs a paint database.</summary>
    public PaintDatabase(
        in ILogger<PaintDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs
    ) : base(logger, fileParseService, vfs)
    {
        _logger = logger;
        Load();

        _registry = PaintRegistry.FromNames([.. _loaded.Select(paint => paint.ID)]);
        _byId = new Paint?[_registry.Count];
        for (var i = 0; i < _loaded.Count; i++)
        {
            Paint paint = _loaded[i];
            paint.BindDataID(_registry.Id(paint.ID));
            _byId[paint.DataID - Paint.MIN_ID] = paint;
        }

        _texturePaints = [.. _byId.OfType<Paint>().Where(p => p.Kind == PaintKind.Texture)];
        _colorPaints = [.. _byId.OfType<Paint>().Where(p => p.Kind == PaintKind.Color)];
    }

    /// <inheritdoc/>
    public IReadOnlyList<Paint> TexturePaints => _texturePaints;

    /// <inheritdoc/>
    public IReadOnlyList<Paint> ColorPaints => _colorPaints;

    /// <inheritdoc/>
    public ushort Id(string name) => _registry.Id(name);

    /// <inheritdoc/>
    public ushort Resolve(string name) => _registry.Resolve(name);

    /// <inheritdoc/>
    public string? Name(ushort id) => _registry.Name(id);

    /// <inheritdoc/>
    public Result<Paint> Get(ushort id)
    {
        Paint? paint = GetOrNull(id);
        return paint != null ? Result<Paint>.FromSuccess(paint) : Result<Paint>.FromFailure($"Unknown paint \"{id}\"");
    }

    /// <inheritdoc/>
    public List<Paint> Get(Func<Paint, bool> predicate)
    {
        return [.. _byId.OfType<Paint>().Where(predicate)];
    }

    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".toml");

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => new("paints/");

    /// <inheritdoc/>
    protected override IEnumerable<PaintDefinition> GetAssetInfo(PathInfo path, PaintDefinitions resource) => resource.Paints ?? [];

    /// <inheritdoc/>
    protected override string GetAssetID(PaintDefinition assetInfo) => assetInfo.ID;

    /// <inheritdoc/>
    protected override Result<Paint> LoadAsset(string id, PaintDefinition assetInfo)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            _logger.LogWarning("Skipping paint definition with empty ID.");
            return Result<Paint>.FromFailure("Paint definition must declare an ID.");
        }

        bool hasTextures = HasNonEmptyTexture(assetInfo.Textures);
        bool hasColor = !string.IsNullOrWhiteSpace(assetInfo.Color);

        if (hasTextures == hasColor)
        {
            _logger.LogWarning("Skipping paint \"{PaintId}\": must define either Textures or Color, not both or neither.", id);
            return Result<Paint>.FromFailure($"Paint \"{id}\" must define either Textures or Color.");
        }

        Paint paint;
        if (hasTextures)
        {
            paint = Paint.CreateTexture(id, assetInfo.Name, assetInfo.Alpha, assetInfo.Connected, assetInfo.Textures!);
        }
        else
        {
            if (!Paint.TryParseHexColor(assetInfo.Color, out Vector3 color))
            {
                _logger.LogWarning("Skipping paint \"{PaintId}\": invalid hex color \"{Color}\".", id, assetInfo.Color);
                return Result<Paint>.FromFailure($"Paint \"{id}\" has invalid hex color \"{assetInfo.Color}\".");
            }

            paint = Paint.CreateColor(id, assetInfo.Name, color);
        }

        _loaded.Add(paint);
        return Result<Paint>.FromSuccess(paint);
    }

    private static bool HasNonEmptyTexture(string?[]? textures)
    {
        if (textures == null || textures.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < textures.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(textures[i]))
            {
                return true;
            }
        }

        return false;
    }

    private Paint? GetOrNull(ushort id)
    {
        if (id < Paint.MIN_ID)
        {
            return null;
        }

        int index = id - Paint.MIN_ID;
        return index < _byId.Length ? _byId[index] : null;
    }
}
