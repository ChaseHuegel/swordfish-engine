using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace WaywardBeyond.Bodies;

/// <inheritdoc/>
internal sealed class BodyDatabase : VirtualAssetDatabase<BodyDefinitions, BodyDefinition, Body>, IBodyDatabase
{
    /// <summary>The direction tags from forward-facing, iterating clockwise around the up axis.</summary>
    private static readonly string[] _orderedDirections = ["front", "back", "left", "right"];
    
    private readonly List<string> _ids = [];

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
    public int Count => _ids.Count;

    /// <inheritdoc/>
    public IReadOnlyList<string> Ids => _ids;

    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".toml");

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => new("bodies/");

    /// <inheritdoc/>
    protected override IEnumerable<BodyDefinition> GetAssetInfo(PathInfo path, BodyDefinitions resource) => resource.Bodies ?? [];

    /// <inheritdoc/>
    protected override string GetAssetID(BodyDefinition assetInfo) => assetInfo.ID ?? string.Empty;

    /// <inheritdoc/>
    protected override Result<Body> LoadAsset(string id, BodyDefinition assetInfo)
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

        var info = new Body(id, states);
        _ids.Add(id);
        DefaultId ??= id;
        return Result<Body>.FromSuccess(info);
    }
    
    /// <summary>
    /// Reads keys as direction tags and values as texture paths.
    /// Multiple texture paths for one direction are flattened.
    /// </summary>
    /// <returns>An array of texture paths in order of supported directions as defined by <see cref="_orderedDirections"/>.</returns>
    private static string[] ResolveOrderedTextures(in Dictionary<string, string?[]> directions)
    {
        var normalizedDirections = new Dictionary<string, string?[]>(directions, StringComparer.InvariantCultureIgnoreCase);
        var textures = new List<string>();
        foreach (string orderedDirection in _orderedDirections)
        {
            if (!normalizedDirections.TryGetValue(orderedDirection, out string?[]? paths))
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