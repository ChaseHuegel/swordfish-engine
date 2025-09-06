using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.Graphics;
using Swordfish.Library.IO;

namespace Swordfish.IO;

/// <summary>
///     Provides access to <see cref="Texture"/>s from virtual resources.
/// </summary>
internal sealed class TextureDatabase : SimpleVirtualAssetDatabase<Texture>, IAutoActivate
{
    public TextureDatabase(
        in ILogger<TextureDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs)
        : base(logger, fileParseService, vfs)
    {
        Load();
    }
    
    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".png");

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => AssetPaths.Textures;
}