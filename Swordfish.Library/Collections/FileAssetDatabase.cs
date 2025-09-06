using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace Swordfish.Library.Collections;

/// <summary>
///     Provides access to assets of type <see cref="TAsset"/>,
///     loaded from files parsed of type <see cref="TFileModel"/>
///     containing one or more of type <see cref="TAssetInfo"/>s.
/// </summary>
public abstract class FileAssetDatabase<TFileModel, TAssetInfo, TAsset>(
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
                    var fileModel = FileParseService.Parse<TFileModel>(file);
                    foreach (TAssetInfo assetInfo in GetAssetInfo(file, fileModel))
                    {
                        string id = GetAssetID(file, assetInfo);
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
    ///     Provides one or more <see cref="TAssetInfo"/>s from a parsed <see cref="TFileModel"/>,
    ///     which will be used to load <see cref="TAsset"/>s by <see cref="LoadAsset"/>.
    /// </summary>
    protected abstract IEnumerable<TAssetInfo> GetAssetInfo(PathInfo path, TFileModel model);
    
    /// <summary>
    ///     Determines a <see cref="TAsset"/>'s unique ID within the database.
    /// </summary>
    protected abstract string GetAssetID(PathInfo path, TAssetInfo assetInfo);
    
    /// <summary>
    ///     Creates or transforms a <see cref="TAsset"/> from the provided <see cref="TAssetInfo"/>.
    /// </summary>
    protected abstract Result<TAsset> LoadAsset(string id, TAssetInfo assetInfo);
}