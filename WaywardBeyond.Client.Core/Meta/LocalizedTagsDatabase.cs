using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.Meta;

internal sealed class LocalizedTagsDatabase : VirtualAssetDatabase<LocalizedTagsDefinition, LocalizedTagsDefinition, LocalizedTags>, IAutoActivate
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

        //  Merge any invariant tags into each language
        foreach ((string key, var localizedTags) in _localizedTags)
        {
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            foreach (KeyValuePair<string, List<string>> invariantTag in invariantTags.Tags)
            {
                if (!localizedTags.Tags.TryGetValue(invariantTag.Key, out List<string>? tags))
                {
                    tags = [];
                    localizedTags.Tags[invariantTag.Key] = tags;
                }
                
                for (var i = 0; i < invariantTag.Value.Count; i++)
                {
                    string tag = invariantTag.Value[i];
                    if (!tags.Contains(tag))
                    {
                        tags.Add(tag);
                    }
                }
            }
        }

        // No longer need the separate invariant entry
        _localizedTags.Remove(string.Empty);
    }
    
    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".toml") || path.HasExtension(".csv");
    
    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => AssetPaths.Root.At("lang");
    
    /// <inheritdoc/>
    protected override IEnumerable<LocalizedTagsDefinition> GetAssetInfo(PathInfo path, LocalizedTagsDefinition resource) => [resource];
    
    /// <inheritdoc/>
    protected override string GetAssetID(LocalizedTagsDefinition assetInfo) => assetInfo.TwoLetterISOLanguageName;
    
    /// <inheritdoc/>
    protected override Result<LocalizedTags> LoadAsset(string id, LocalizedTagsDefinition assetInfo)
    {
        if (!_localizedTags.TryGetValue(id, out LocalizedTags? localizedTags))
        {
            //  This lang key doesn't exist, create it
            localizedTags = new LocalizedTags(assetInfo.Tags);
            _localizedTags.Add(id, localizedTags);
            return Result<LocalizedTags>.FromSuccess(localizedTags);
        }
        
        //  This lang key already exists, append the tags
        foreach (KeyValuePair<string, List<string>> localizedTag in assetInfo.Tags)
        {
            if (!localizedTags.Tags.TryGetValue(localizedTag.Key, out List<string>? tags))
            {
                tags = [];
                localizedTags.Tags[localizedTag.Key] = tags;
            }

            for (var i = 0; i < localizedTag.Value.Count; i++)
            {
                string tag = localizedTag.Value[i];
                if (!tags.Contains(tag))
                {
                    tags.Add(tag);
                }
            }
        }
        
        return Result<LocalizedTags>.FromSuccess(localizedTags);
    }
}