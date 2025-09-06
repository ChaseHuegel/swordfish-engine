using Microsoft.Extensions.Logging;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace Swordfish.IO;

/// <summary>
///     Provides access to assets from virtual resources that are represented by the same, singular type.
/// </summary>
/// <typeparam name="TAsset">
///     The asset's type; which must be parseable by <see cref="IFileParseService"/>.
/// </typeparam>
public abstract class SimpleVirtualAssetDatabase<TAsset>(
    in ILogger logger,
    in IFileParseService fileParseService,
    in VirtualFileSystem vfs)
    : VirtualAssetDatabase<TAsset, Resource<TAsset>, TAsset>(logger, fileParseService, vfs)
{
    /// <inheritdoc/>
    protected override IEnumerable<Resource<TAsset>> GetAssetInfo(PathInfo path, TAsset resource)
    {
        //  A resource only represents a singular asset.
        yield return new Resource<TAsset>(path, resource);
    }

    /// <inheritdoc/>
    protected override string GetAssetID(Resource<TAsset> assetInfo)
    {
        //  Simple assets have an ID that is their file name and relative path from the
        //  root folder. Since files come from VFS they may not share a common full
        //  root path, and need to be re-rooted relative to the virtual root folder
        //  in order to get the relative path to root for use as their ID.
        //  
        //  Example:
        //      fullPath            = "modules/swordfish/assets/textures/some/path/image.png"
        //      virtualRoot         = "textures/"
        //      virtualPath         = "textures/some/path/image.png"
        //      virtualRelativePath = "some/path/image.png"
        var fullPath = assetInfo.SourcePath.ToString();
        string virtualRoot = GetRootPath();
        int indexOfRoot = fullPath.IndexOf(virtualRoot, StringComparison.Ordinal);
        string virtualPath = fullPath[indexOfRoot..];
        string virtualRelativePath = Path.GetRelativePath(virtualRoot, virtualPath);
        
        //  Ensure consistent separates are used since this 
        //  is an ID and should not vary by platform.
        return virtualRelativePath.Replace('\\', '/');
    }

    /// <inheritdoc/>
    protected override Result<TAsset> LoadAsset(string id, Resource<TAsset> assetInfo)
    {
        return Result<TAsset>.FromSuccess(assetInfo.Value);
    }
}