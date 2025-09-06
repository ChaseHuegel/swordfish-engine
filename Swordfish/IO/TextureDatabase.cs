using Microsoft.Extensions.Logging;
using Shoal.Modularity;
using Swordfish.Graphics;
using Swordfish.Library.IO;

namespace Swordfish.IO;

/// <summary>
///     Provides access to <see cref="Texture"/>s from virtual resources.
/// </summary>
internal sealed class TextureDatabase(
    in ILogger<TextureDatabase> logger,
    in IFileParseService fileParseService,
    in VirtualFileSystem vfs)
    : SimpleVirtualAssetDatabase<Texture>(logger, fileParseService, vfs), IEntryPoint
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
}