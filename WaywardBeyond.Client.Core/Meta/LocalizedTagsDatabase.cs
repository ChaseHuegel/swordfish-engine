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
    }
    
    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".toml");
    
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
            
            tags.AddRange(localizedTag.Value);
        }
        
        return Result<LocalizedTags>.FromSuccess(localizedTags);
    }
}