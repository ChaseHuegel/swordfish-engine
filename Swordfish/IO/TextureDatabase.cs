using Microsoft.Extensions.Logging;
using Shoal.Modularity;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace Swordfish.IO;

/// <inheritdoc cref="FileAssetDatabase{TFileModel,TAssetInfo,TAsset}"/>
internal sealed class TextureDatabase(
    in ILogger<TextureDatabase> logger,
    in IFileParseService fileParseService,
    in VirtualFileSystem vfs)
    : FileAssetDatabase<Texture, Texture, Texture>(logger, fileParseService, vfs), IEntryPoint
{
    /// <inheritdoc/>
    public void Run()
    {
        Load();
    }
    
    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".png");

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => AssetPaths.Textures;

    /// <inheritdoc/>
    protected override IEnumerable<Texture> GetAssetInfo(PathInfo path, Texture model)
    {
        yield return model;
    }

    /// <inheritdoc/>
    protected override string GetAssetID(PathInfo path, Texture assetInfo)
    {
        //  Textures have an ID that is their file name and relative path from the
        //  Textures folder. Since files come from VFS they may not share a common full
        //  root path, and need to be re-rooted relative to the virtual Textures folder
        //  in order to get the relative path to Textures for use as their ID.
        //  
        //  Example:
        //      fullPath            = "modules/swordfish/assets/textures/some/path/image.png"
        //      virtualRoot         = "textures/"
        //      virtualPath         = "textures/some/path/image.png"
        //      virtualRelativePath = "some/path/image.png"
        var fullPath = path.ToString();
        string virtualRoot = GetRootPath();
        int indexOfRoot = fullPath.IndexOf(virtualRoot, StringComparison.Ordinal);
        string virtualPath = fullPath[indexOfRoot..];
        string virtualRelativePath = Path.GetRelativePath(virtualRoot, virtualPath);
        
        //  Ensure consistent separates are used since this 
        //  is an ID and should not vary by platform.
        return virtualRelativePath.Replace('\\', '/');
    }

    /// <inheritdoc/>
    protected override Result<Texture> LoadAsset(string id, Texture assetInfo)
    {
        return Result<Texture>.FromSuccess(assetInfo);
    }
}