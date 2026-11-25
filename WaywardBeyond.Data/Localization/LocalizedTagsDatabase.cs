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

            localizedTags.Merge(invariantTags);
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
    protected override string GetAssetID(LocalizedTagsDefinition assetInfo) => assetInfo.TwoLetterISOLanguageName ?? string.Empty;

    /// <inheritdoc/>
    protected override Result<LocalizedTags> LoadAsset(string id, LocalizedTagsDefinition assetInfo)
    {
        if (assetInfo.Tags == null)
        {
            return Result<LocalizedTags>.FromFailure("Tag file carries no tags");
        }

        if (!_localizedTags.TryGetValue(id, out LocalizedTags? localizedTags))
        {
            //  This lang key does not exist yet. Create it.
            localizedTags = new LocalizedTags(assetInfo.Tags);
            _localizedTags.Add(id, localizedTags);
            return Result<LocalizedTags>.FromSuccess(localizedTags);
        }

        //  This lang key already exists. Append the tags.
        localizedTags.Merge(assetInfo.Tags);

        return Result<LocalizedTags>.FromSuccess(localizedTags);
    }
}