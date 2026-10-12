using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace WaywardBeyond.Data.Localization;

/// <summary>
///     Loads tag lists from <c>lang/**/tags/</c>.
/// </summary>
internal sealed class LocalizedTagsDatabase : VirtualAssetDatabase<LocalizedTagsDefinition, LocalizedTagsDefinition, LocalizedTags>, IAutoActivate, ILocalizedTagsDatabase
{
    private readonly Dictionary<string, LocalizedTags> _localizedTags = [];

    public LocalizedTagsDatabase(
        in ILogger<LocalizedTagsDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs
    ) : base(logger, fileParseService, vfs)
    {
        Load();

        if (!_localizedTags.TryGetValue(string.Empty, out LocalizedTags? invariantTags))
        {
            return;
        }

        //  Merge the invariant tags into each language so localized lookups see them.
        foreach ((string key, LocalizedTags localizedTags) in _localizedTags)
        {
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            MergeTags(destination: localizedTags.Tags, source: invariantTags.Tags);
        }
    }

    /// <inheritdoc/>
    public LocalizedTags? Invariant => _localizedTags.GetValueOrDefault(string.Empty);

    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.Value.Contains("/tags/") && (path.HasExtension(".toml") || path.HasExtension(".csv"));

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => new("lang/");

    /// <inheritdoc/>
    protected override IEnumerable<LocalizedTagsDefinition> GetAssetInfo(PathInfo path, LocalizedTagsDefinition resource) => [resource];

    /// <inheritdoc/>
    protected override string GetAssetID(LocalizedTagsDefinition assetInfo) => assetInfo.TwoLetterISOLanguageName;

    /// <inheritdoc/>
    protected override Result<LocalizedTags> LoadAsset(string id, LocalizedTagsDefinition assetInfo)
    {
        if (!_localizedTags.TryGetValue(id, out LocalizedTags? localizedTags))
        {
            //  This lang key does not exist yet. Create it.
            localizedTags = new LocalizedTags(assetInfo.Tags);
            _localizedTags.Add(id, localizedTags);
            return Result<LocalizedTags>.FromSuccess(localizedTags);
        }

        //  This lang key already exists. Append the tags.
        if (assetInfo.Tags != null)
        {
            MergeTags(destination: localizedTags.Tags, source: assetInfo.Tags);
        }

        return Result<LocalizedTags>.FromSuccess(localizedTags);
    }

    private static void MergeTags(Dictionary<string, List<string>> destination, Dictionary<string, List<string>> source)
    {
        foreach (KeyValuePair<string, List<string>> tag in source)
        {
            if (!destination.TryGetValue(tag.Key, out List<string>? values))
            {
                values = [];
                destination[tag.Key] = values;
            }

            for (var i = 0; i < tag.Value.Count; i++)
            {
                string value = tag.Value[i];
                if (!values.Contains(value))
                {
                    values.Add(value);
                }
            }
        }
    }
}