using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.Graphics;
using Swordfish.Library.IO;

namespace Swordfish.IO;

/// <summary>
///     Provides access to <see cref="Mesh"/>s from virtual resources.
/// </summary>
internal sealed class MeshDatabase : SimpleVirtualAssetDatabase<Mesh>, IAutoActivate
{
    public MeshDatabase(
        in ILogger<MeshDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs)
        : base(logger, fileParseService, vfs)
    {
        Load();
    }
    
    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".obj");

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => AssetPaths.Meshes;
}