using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace Swordfish.Library.Collections;

/// <summary>
///     Provides access to assets from virtual resources that may represent differing types virtually and in memory.
/// </summary>
/// <typeparam name="TResource">
///     The resource's type; which must be parseable by <see cref="IFileParseService"/>.
/// </typeparam>
/// <typeparam name="TAssetInfo">
///     The type that will be created or extracted from <typeparamref name="TResource"/>
///     which provides necessary information to load the <typeparamref name="TAsset"/>.
///     One of more of these may be representable by a single <typeparamref name="TResource"/>,
///     allowing implementations to extract multiple assets from a single resource.
/// </typeparam>
/// <typeparam name="TAsset">
///     The asset's type.
/// </typeparam>
public abstract class VirtualAssetDatabase<TResource, TAssetInfo, TAsset>(
    in ILogger logger,
    in IFileParseService fileParseService,
    in VirtualFileSystem vfs)
    : IAssetDatabase<TAsset>
{
    protected readonly ILogger Logger = logger;
    protected readonly IFileParseService FileParseService = fileParseService;
    protected readonly VirtualFileSystem VFS = vfs;
    
    private readonly Dictionary<string, TAsset> _assets = [];
    
    /// <inheritdoc/>
    public Result<TAsset> Get(string id)
    {
        lock (_assets)
        {
            if (_assets.TryGetValue(id, out TAsset value))
            {
                return Result<TAsset>.FromSuccess(value);
            }

            return Result<TAsset>.FromFailure($"Unknown asset \"{id}\"");
        }
    }
    
    /// <summary>
    ///     Loads all assets into the database.
    /// </summary>
    protected void Load()
    {
        lock (_assets)
        {
            List<PathInfo> files = VFS.GetFiles(GetRootPath(), SearchOption.AllDirectories).Where(IsValidFile).ToList();
            foreach (PathInfo file in files)
            {
                try
                {
                    var resource = FileParseService.Parse<TResource>(file);
                    foreach (TAssetInfo assetInfo in GetAssetInfo(file, resource))
                    {
                        string id = GetAssetID(assetInfo);
                        Result<TAsset> assetResult = LoadAsset(id, assetInfo);
                        if (!assetResult)
                        {
                            Logger.LogError(assetResult, "Failed to load {assetInfo} from \"{file}\".", typeof(TAssetInfo).Name, file);
                            continue;
                        }
                        
                        _assets[id] = assetResult;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to load {assetInfo} from \"{file}\".", typeof(TAssetInfo).Name, file);
                }
            }

            Logger.LogInformation("Loaded {count} {asset}s from {fileCount} files.", _assets.Count, typeof(TAsset).Name, files.Count);
        }
    }

    /// <summary>
    ///     Whether a file at the provided path is valid to attempt loading.
    /// </summary>
    protected abstract bool IsValidFile(PathInfo path);
    
    /// <summary>
    ///     Provides the root path to search for assets recursively.
    /// </summary>
    protected abstract PathInfo GetRootPath();
    
    /// <summary>
    ///     Provides one or more <typeparamref name="TAssetInfo"/>s from a parsed <typeparamref name="TResource"/>,
    ///     which will be used to load <typeparamref name="TAsset"/>s by <typeparamref name="LoadAsset"/>.
    /// </summary>
    protected abstract IEnumerable<TAssetInfo> GetAssetInfo(PathInfo path, TResource resource);
    
    /// <summary>
    ///     Determines a <typeparamref name="TAsset"/>'s unique ID within the database.
    /// </summary>
    protected abstract string GetAssetID(TAssetInfo assetInfo);
    
    /// <summary>
    ///     Creates or transforms a <typeparamref name="TAsset"/> from the provided <typeparamref name="TAssetInfo"/>.
    /// </summary>
    protected abstract Result<TAsset> LoadAsset(string id, TAssetInfo assetInfo);
}