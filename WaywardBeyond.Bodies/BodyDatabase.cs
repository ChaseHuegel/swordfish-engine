using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace WaywardBeyond.Bodies;

/// <inheritdoc/>
internal sealed class BodyDatabase : VirtualAssetDatabase<BodyDefinitions, BodyDefinition, BodyInfo>, IBodyDatabase
{
    /// <summary>The direction tags from forward-facing, iterating clockwise around the up axis.</summary>
    private static readonly string[] _orderedDirections = ["front", "back", "left", "right"];
    
    private readonly Dictionary<string, BodyDefinition> _models = [];

    /// <summary>Constructs a body database.</summary>
    public BodyDatabase(
        in ILogger<BodyDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs
    ) : base(logger, fileParseService, vfs)
    {
        Load();
    }
    
    /// <inheritdoc/>
    public string? DefaultId { get; private set; }

    /// <inheritdoc/>
    public int Count => _models.Count;

    /// <inheritdoc/>
    public IEnumerable<string> Ids => _models.Keys;

    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".toml");

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => new("bodies/");

    /// <inheritdoc/>
    protected override IEnumerable<BodyDefinition> GetAssetInfo(PathInfo path, BodyDefinitions resource) => resource.Bodies ?? [];

    /// <inheritdoc/>
    protected override string GetAssetID(BodyDefinition assetInfo) => assetInfo.ID ?? string.Empty;

    /// <inheritdoc/>
    protected override Result<BodyInfo> LoadAsset(string id, BodyDefinition assetInfo)
    {
        var states = new Dictionary<string, string[]>();
        if (assetInfo.States != null)
        {
            foreach ((string tag, Dictionary<string, string?[]> directions) in assetInfo.States)
            {
                string normalizedTag = tag.ToLowerInvariant();
                states[normalizedTag] = ResolveOrderedTextures(in directions);
            }
        }

        var info = new BodyInfo(id, states);
        _models[id] = assetInfo;
        DefaultId ??= id;
        return Result<BodyInfo>.FromSuccess(info);
    }
    
    /// <summary>
    /// Reads keys as direction tags and values as texture paths.
    /// Multiple texture paths for one direction are flattened.
    /// </summary>
    /// <returns>An array of texture paths in order of supported directions as defined by <see cref="_orderedDirections"/>.</returns>
    private static string[] ResolveOrderedTextures(in Dictionary<string, string?[]> directions)
    {
        var textures = new List<string>();
        foreach (string direction in _orderedDirections)
        {
            string normalizedDirection = direction.ToLowerInvariant();
            if (!directions.TryGetValue(normalizedDirection, out string?[]? paths))
            {
                continue;
            }

            foreach (string? path in paths)
            {
                if (!string.IsNullOrEmpty(path))
                {
                    textures.Add(path);
                }
            }
        }

        return [.. textures];
    }
}