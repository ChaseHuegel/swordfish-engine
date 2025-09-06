using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Shoal.Modularity;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Items;

namespace WaywardBeyond.Client.Core.Bricks;

/// <summary>
///     Provides access to brick information from virtual resources.
/// </summary>
internal sealed class BrickDatabase : VirtualAssetDatabase<BrickDefinitions, BrickDefinition, BrickDefinition>, IEntryPoint
{
    public BrickDatabase(
        in ILogger<ItemDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs)
        : base(logger, fileParseService, vfs)
    {
    }
    
    /// <inheritdoc/>
    public void Run()
    {
        Load();
    }
    
    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".toml");
    
    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => AssetPaths.Root.At("bricks");
    
    /// <inheritdoc/>
    protected override IEnumerable<BrickDefinition> GetAssetInfo(PathInfo path, BrickDefinitions resource) => resource.Bricks;

    /// <inheritdoc/>
    protected override string GetAssetID(BrickDefinition assetInfo) => assetInfo.ID;
    
    /// <inheritdoc/>
    protected override Result<BrickDefinition> LoadAsset(string id, BrickDefinition assetInfo)
    {
        return Result<BrickDefinition>.FromSuccess(assetInfo);
    }
}