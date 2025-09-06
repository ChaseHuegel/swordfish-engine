using Microsoft.Extensions.Logging;
using Shoal.Modularity;
using Swordfish.Graphics;
using Swordfish.Library.IO;

namespace Swordfish.IO;

/// <summary>
///     Provides access to <see cref="Shader"/>s from virtual resources.
/// </summary>
internal sealed class ShaderDatabase(
    in ILogger<ShaderDatabase> logger,
    in IFileParseService fileParseService,
    in VirtualFileSystem vfs)
    : SimpleVirtualAssetDatabase<Shader>(logger, fileParseService, vfs), IEntryPoint
{
    /// <inheritdoc/>
    public void Run()
    {
        Load();
    }
    
    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".glsl");

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => AssetPaths.Shaders;
}