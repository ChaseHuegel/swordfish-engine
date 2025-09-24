using Microsoft.Extensions.Logging;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace Swordfish.IO;

/// <summary>
///     Provides access to assets from virtual resources by relative path that are parseable into their type.
/// </summary>
/// <typeparam name="TAsset">
///     The asset's type; which must be parseable by <see cref="IFileParseService"/>.
/// </typeparam>
public abstract class SimpleVirtualAssetDatabase<TAsset>(
    in ILogger logger,
    in IFileParseService fileParseService,
    in VirtualFileSystem vfs)
    : ResourceVirtualAssetDatabase<TAsset, TAsset>(logger, fileParseService, vfs)
{
    /// <inheritdoc/>
    protected override Result<TAsset> LoadAsset(string id, Resource<TAsset> assetInfo)
    {
        return Result<TAsset>.FromSuccess(assetInfo.Value);
    }
}