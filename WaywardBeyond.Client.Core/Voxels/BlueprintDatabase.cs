using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.IO;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Voxels;

/// <summary>
///     Provides access to <see cref="VoxelEntityModel"/>s from virtual resources.
/// </summary>
internal sealed class BlueprintDatabase : SimpleVirtualAssetDatabase<VoxelEntityModel>, IAutoActivate
{
    public BlueprintDatabase(
        in ILogger<BlueprintDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs)
        : base(logger, fileParseService, vfs)
    {
        Load();
    }
    
    /// <inheritdoc/>
    protected override bool ExcludeExtensionFromID => true;
    
    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".dat");

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => AssetPaths.Root.At("blueprints");
}