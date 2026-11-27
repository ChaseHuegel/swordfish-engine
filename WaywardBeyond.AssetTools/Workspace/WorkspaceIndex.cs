using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Documents;

namespace WaywardBeyond.AssetTools.Workspace;

/// <summary>
///     Multi-root content, resource, localization, tag, and inbound-reference index for an opened workspace.
/// </summary>
public sealed class WorkspaceIndex
{
    private readonly Dictionary<string, List<InboundReference>> _inboundByTarget;
    private readonly Dictionary<(ResourceKind Kind, string LowerId), IndexedResource> _resourcesCaseInsensitive;
    private readonly Dictionary<string, IndexedResource> _albedoCaseInsensitive;
    private readonly Dictionary<string, string> _winningBlueprintFiles;
    private readonly Dictionary<string, string> _blueprintsCaseInsensitive;

    /// <summary>
    ///     Creates an immutable workspace index from loaded roots, assets, resources, and references.
    /// </summary>
    public WorkspaceIndex(
        WorkspaceMode mode,
        string rootPath,
        string? referenceInstallPath,
        IReadOnlyList<WorkspaceRoot> roots,
        ModulesConfigData? modulesConfig,
        IReadOnlyList<IndexedAsset> allAssets,
        IReadOnlyDictionary<(AssetKind Kind, string Id), IndexedAsset> winningAssets,
        IReadOnlyList<IndexedResource> allResources,
        IReadOnlyDictionary<(ResourceKind Kind, string Id), IndexedResource> winningResources,
        IReadOnlyDictionary<string, IndexedResource> winningBlockAlbedoLayers,
        IReadOnlyList<string> orderedAlbedoLayerNames,
        IReadOnlyList<IndexedLocalizationEntry> localizationEntries,
        IReadOnlyList<IndexedTagEntry> tagEntries,
        IReadOnlyDictionary<string, TomlDocument> documentsByPath,
        IReadOnlyList<InboundReference> allReferences,
        IReadOnlyList<string> blueprintFiles)
    {
        Mode = mode;
        RootPath = rootPath;
        ReferenceInstallPath = referenceInstallPath;
        Roots = roots;
        ModulesConfig = modulesConfig;
        AllAssets = allAssets;
        WinningAssets = winningAssets;
        AllResources = allResources;
        WinningResources = winningResources;
        WinningBlockAlbedoLayers = winningBlockAlbedoLayers;
        OrderedAlbedoLayerNames = orderedAlbedoLayerNames;
        LocalizationEntries = localizationEntries;
        TagEntries = tagEntries;
        DocumentsByPath = documentsByPath;
        AllReferences = allReferences;
        BlueprintFiles = blueprintFiles;

        _inboundByTarget = new Dictionary<string, List<InboundReference>>(StringComparer.Ordinal);
        for (int i = 0; i < allReferences.Count; i++)
        {
            InboundReference reference = allReferences[i];
            if (!_inboundByTarget.TryGetValue(reference.TargetId, out List<InboundReference>? list))
            {
                list = [];
                _inboundByTarget[reference.TargetId] = list;
            }

            list.Add(reference);
        }

        _resourcesCaseInsensitive = new Dictionary<(ResourceKind, string), IndexedResource>();
        foreach (KeyValuePair<(ResourceKind Kind, string Id), IndexedResource> kvp in winningResources)
        {
            _resourcesCaseInsensitive[(kvp.Key.Kind, kvp.Key.Id.ToLowerInvariant())] = kvp.Value;
        }

        _albedoCaseInsensitive = new Dictionary<string, IndexedResource>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, IndexedResource> kvp in winningBlockAlbedoLayers)
        {
            _albedoCaseInsensitive[kvp.Key] = kvp.Value;
        }

        _winningBlueprintFiles = new Dictionary<string, string>(StringComparer.Ordinal);
        _blueprintsCaseInsensitive = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < blueprintFiles.Count; i++)
        {
            string fullPath = blueprintFiles[i];
            string? blueprintId = TryExtractBlueprintId(roots, fullPath);
            if (string.IsNullOrWhiteSpace(blueprintId))
            {
                continue;
            }

            _winningBlueprintFiles[blueprintId] = fullPath;
            _blueprintsCaseInsensitive[blueprintId] = blueprintId;
        }
    }

    /// <summary>
    ///     Gets whether the workspace is in Repo or Pack mode.
    /// </summary>
    public WorkspaceMode Mode { get; }

    /// <summary>
    ///     Gets the primary opened workspace directory path.
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    ///     Gets the optional reference game install path used in Pack mode.
    /// </summary>
    public string? ReferenceInstallPath { get; }

    /// <summary>
    ///     Gets all discovered workspace roots in load order.
    /// </summary>
    public IReadOnlyList<WorkspaceRoot> Roots { get; }

    /// <summary>
    ///     Gets the parsed <c>config/modules.toml</c> data when available.
    /// </summary>
    public ModulesConfigData? ModulesConfig { get; }

    /// <summary>
    ///     Gets every asset occurrence across all enabled roots including shadowed definitions.
    /// </summary>
    public IReadOnlyList<IndexedAsset> AllAssets { get; }

    /// <summary>
    ///     Gets the winning asset per <c>(AssetKind, Id)</c> after load-order resolution.
    /// </summary>
    public IReadOnlyDictionary<(AssetKind Kind, string Id), IndexedAsset> WinningAssets { get; }

    /// <summary>
    ///     Gets every resource occurrence across all enabled roots.
    /// </summary>
    public IReadOnlyList<IndexedResource> AllResources { get; }

    /// <summary>
    ///     Gets the winning resource per <c>(ResourceKind, Id)</c> after mount-order resolution.
    /// </summary>
    public IReadOnlyDictionary<(ResourceKind Kind, string Id), IndexedResource> WinningResources { get; }

    /// <summary>
    ///     Gets winning block albedo layers keyed by bare filename stem without extension.
    /// </summary>
    public IReadOnlyDictionary<string, IndexedResource> WinningBlockAlbedoLayers { get; }

    /// <summary>
    ///     Gets the natural-sorted block albedo layer list with <c>"missing"</c> at layer index 0.
    /// </summary>
    public IReadOnlyList<string> OrderedAlbedoLayerNames { get; }

    /// <summary>
    ///     Gets all localization translation entries across enabled roots in load order.
    /// </summary>
    public IReadOnlyList<IndexedLocalizationEntry> LocalizationEntries { get; }

    /// <summary>
    ///     Gets all tag list entries across enabled roots in load order.
    /// </summary>
    public IReadOnlyList<IndexedTagEntry> TagEntries { get; }

    /// <summary>
    ///     Gets all parsed TOML documents indexed by full file path.
    /// </summary>
    public IReadOnlyDictionary<string, TomlDocument> DocumentsByPath { get; }

    /// <summary>
    ///     Gets every outgoing reference extracted from winning definitions and tags.
    /// </summary>
    public IReadOnlyList<InboundReference> AllReferences { get; }

    /// <summary>
    ///     Gets all discovered blueprint <c>.wbp</c> file paths across enabled roots.
    /// </summary>
    public IReadOnlyList<string> BlueprintFiles { get; }

    /// <summary>
    ///     Gets winning bundled blueprint file paths keyed by normalized relative blueprint ID (without <c>.wbp</c>).
    /// </summary>
    public IReadOnlyDictionary<string, string> WinningBlueprintFiles => _winningBlueprintFiles;

    /// <summary>
    ///     Gets the winning bundled blueprint file path matching case-exact <paramref name="blueprintId"/>.
    /// </summary>
    public Result<string> GetBlueprintFile(string blueprintId)
    {
        if (!string.IsNullOrEmpty(blueprintId) && _winningBlueprintFiles.TryGetValue(blueprintId, out string? fullPath))
        {
            return Result<string>.FromSuccess(fullPath);
        }

        return Result<string>.FromFailure($"Blueprint '{blueprintId}' was not found.");
    }

    /// <summary>
    ///     Finds a winning bundled blueprint ID matching <paramref name="blueprintId"/> case-insensitively to detect case drift.
    /// </summary>
    public Result<string> FindBlueprintIdCaseInsensitive(string blueprintId)
    {
        if (!string.IsNullOrEmpty(blueprintId) && _blueprintsCaseInsensitive.TryGetValue(blueprintId, out string? canonicalId))
        {
            return Result<string>.FromSuccess(canonicalId);
        }

        return Result<string>.FromFailure($"Blueprint '{blueprintId}' was not found.");
    }

    private static string? TryExtractBlueprintId(IReadOnlyList<WorkspaceRoot> roots, string fullPath)
    {
        string normalizedFull = Path.GetFullPath(fullPath);
        StringComparison pathComparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        string? matchedBlueprintsDir = null;
        for (int i = 0; i < roots.Count; i++)
        {
            WorkspaceRoot root = roots[i];
            if (!root.IsEnabled || string.IsNullOrWhiteSpace(root.AssetsDirectory))
            {
                continue;
            }

            string blueprintsDir = Path.GetFullPath(Path.Combine(root.AssetsDirectory, "blueprints"))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string prefix = blueprintsDir + Path.DirectorySeparatorChar;
            if (normalizedFull.StartsWith(prefix, pathComparison))
            {
                matchedBlueprintsDir = blueprintsDir;
            }
        }

        if (matchedBlueprintsDir == null)
        {
            return null;
        }

        string relative = Path.GetRelativePath(matchedBlueprintsDir, normalizedFull).Replace('\\', '/');
        if (relative.EndsWith(".wbp", StringComparison.OrdinalIgnoreCase))
        {
            relative = relative[..^4];
        }

        return relative;
    }

    /// <summary>
    ///     Gets the winning asset matching <paramref name="kind"/> and <paramref name="id"/>.
    /// </summary>
    public Result<IndexedAsset> GetAsset(AssetKind kind, string id)
    {
        if (WinningAssets.TryGetValue((kind, id), out IndexedAsset asset))
        {
            return Result<IndexedAsset>.FromSuccess(asset);
        }

        return Result<IndexedAsset>.FromFailure($"Asset '{id}' of kind '{kind}' was not found.");
    }

    /// <summary>
    ///     Gets all winning assets of <paramref name="kind"/> ordered by identifier.
    /// </summary>
    public IReadOnlyList<IndexedAsset> GetWinningAssetsByKind(AssetKind kind)
    {
        List<IndexedAsset> list = [];
        foreach (IndexedAsset asset in WinningAssets.Values)
        {
            if (asset.Kind == kind)
            {
                list.Add(asset);
            }
        }

        list.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Id, right.Id));
        return list;
    }

    /// <summary>
    ///     Gets the winning resource matching <paramref name="kind"/> and case-exact <paramref name="id"/>.
    /// </summary>
    public Result<IndexedResource> GetResource(ResourceKind kind, string id)
    {
        if (WinningResources.TryGetValue((kind, id), out IndexedResource resource))
        {
            return Result<IndexedResource>.FromSuccess(resource);
        }

        return Result<IndexedResource>.FromFailure($"Resource '{id}' of kind '{kind}' was not found.");
    }

    /// <summary>
    ///     Finds a winning resource matching <paramref name="id"/> case-insensitively to detect case drift.
    /// </summary>
    public Result<IndexedResource> FindResourceCaseInsensitive(ResourceKind kind, string id)
    {
        if (_resourcesCaseInsensitive.TryGetValue((kind, id.ToLowerInvariant()), out IndexedResource resource))
        {
            return Result<IndexedResource>.FromSuccess(resource);
        }

        return Result<IndexedResource>.FromFailure($"Resource '{id}' of kind '{kind}' was not found.");
    }

    /// <summary>
    ///     Finds a winning block albedo layer case-insensitively to detect case drift.
    /// </summary>
    public Result<IndexedResource> FindBlockAlbedoCaseInsensitive(string layerName)
    {
        if (_albedoCaseInsensitive.TryGetValue(layerName, out IndexedResource resource))
        {
            return Result<IndexedResource>.FromSuccess(resource);
        }

        return Result<IndexedResource>.FromFailure($"Block albedo layer '{layerName}' was not found.");
    }

    /// <summary>
    ///     Gets all inbound references pointing to <paramref name="targetId"/>.
    /// </summary>
    public IReadOnlyList<InboundReference> GetInboundReferences(string targetId)
    {
        if (string.IsNullOrEmpty(targetId))
        {
            return [];
        }

        return _inboundByTarget.TryGetValue(targetId, out List<InboundReference>? list)
            ? list
            : [];
    }

    /// <summary>
    ///     Returns true when <paramref name="key"/> is defined in <paramref name="locale"/> localization tables.
    /// </summary>
    public bool HasLocalizationKey(string key, string locale = "en")
    {
        for (int i = 0; i < LocalizationEntries.Count; i++)
        {
            IndexedLocalizationEntry entry = LocalizationEntries[i];
            if (string.Equals(entry.Locale, locale, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.Key, key, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Returns the merged effective values for <paramref name="tagName"/> combining invariant and locale tags.
    /// </summary>
    public IReadOnlyList<string> GetEffectiveTagValues(string tagName, string? locale = null)
    {
        List<string> values = [];
        for (int i = 0; i < TagEntries.Count; i++)
        {
            IndexedTagEntry entry = TagEntries[i];
            if (!string.Equals(entry.TagName, tagName, StringComparison.Ordinal))
            {
                continue;
            }

            bool isInvariant = string.IsNullOrEmpty(entry.Locale);
            bool matchesLocale = !string.IsNullOrEmpty(locale) &&
                                 string.Equals(entry.Locale, locale, StringComparison.OrdinalIgnoreCase);
            if (!isInvariant && !matchesLocale)
            {
                continue;
            }

            for (int j = 0; j < entry.Values.Count; j++)
            {
                values.Add(entry.Values[j]);
            }
        }

        return values;
    }

    /// <summary>
    ///     Returns true when any invariant or matching-locale tag list declares <paramref name="tagName"/>.
    /// </summary>
    public bool HasTag(string tagName, string? locale = null)
    {
        for (int i = 0; i < TagEntries.Count; i++)
        {
            IndexedTagEntry entry = TagEntries[i];
            if (!string.Equals(entry.TagName, tagName, StringComparison.Ordinal))
            {
                continue;
            }

            bool isInvariant = string.IsNullOrEmpty(entry.Locale);
            bool matchesLocale = !string.IsNullOrEmpty(locale) &&
                                 string.Equals(entry.Locale, locale, StringComparison.OrdinalIgnoreCase);
            if (isInvariant || matchesLocale)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Gets the writable root directory for Pack mode or the first writable root in Repo mode.
    /// </summary>
    public Result<WorkspaceRoot> GetPrimaryWritableRoot()
    {
        for (int i = Roots.Count - 1; i >= 0; i--)
        {
            if (Roots[i].IsWritable && Roots[i].IsEnabled)
            {
                return Result<WorkspaceRoot>.FromSuccess(Roots[i]);
            }
        }

        return Result<WorkspaceRoot>.FromFailure("Workspace has no writable root.");
    }
}
