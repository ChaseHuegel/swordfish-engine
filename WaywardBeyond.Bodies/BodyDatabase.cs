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

    private readonly List<string> _species = [];
    private readonly Dictionary<string, List<Body>> _variantsBySpecies = new(StringComparer.InvariantCultureIgnoreCase);
    private readonly Dictionary<string, Gender[]> _gendersBySpecies = new(StringComparer.InvariantCultureIgnoreCase);

    /// <summary>Constructs a body database.</summary>
    public BodyDatabase(
        in ILogger<BodyDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs
    ) : base(logger, fileParseService, vfs)
    {
        Load();
        _species.Sort(new NaturalComparer());

        foreach ((string species, List<Body> variants) in _variantsBySpecies)
        {
            _gendersBySpecies[species] = ResolveGenders(variants);
        }
    }

    /// <inheritdoc/>
    public string? DefaultId { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyList<string> Species => _species;

    /// <inheritdoc/>
    public IReadOnlyList<Gender> GetGenders(string species)
    {
        return _gendersBySpecies.TryGetValue(species, out Gender[]? genders) ? genders : [];
    }

    /// <inheritdoc/>
    public Body? GetBody(string species, Gender gender)
    {
        if (!_variantsBySpecies.TryGetValue(species, out List<Body>? variants) || variants.Count == 0)
        {
            return null;
        }

        foreach (Body variant in variants)
        {
            if (variant.Gender == gender)
            {
                return variant;
            }
        }

        return variants[0];
    }

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

        string species = string.IsNullOrWhiteSpace(assetInfo.Species) ? id : assetInfo.Species;
        var info = new Body(id, assetInfo.Gender, states);
        DefaultId ??= id;

        if (!_variantsBySpecies.TryGetValue(species, out List<Body>? variants))
        {
            variants = [];
            _variantsBySpecies[species] = variants;
            _species.Add(species);
        }

        variants.Add(info);
        return Result<Body>.FromSuccess(info);
    }

    /// <summary>
    ///     Reads keys as direction tags and values as texture paths.
    ///     Multiple texture paths for one direction are flattened.
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

    /// <summary>Collects the distinct genders of a species, ordered by <see cref="Gender"/>.</summary>
    private static Gender[] ResolveGenders(List<Body> variants)
    {
        List<Gender> genders = [];
        foreach (Body variant in variants)
        {
            if (!genders.Contains(variant.Gender))
            {
                genders.Add(variant.Gender);
            }
        }

        genders.Sort();
        return [.. genders];
    }
}
