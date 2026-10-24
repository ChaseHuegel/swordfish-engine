using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace WaywardBeyond.Items;

/// <inheritdoc/>
public sealed class ItemDatabase : VirtualAssetDatabase<ItemDefinitions, ItemDefinition, Item>, IAutoActivate, IItemDatabase
{
    /// <summary>Constructs the item database from the module's item assets.</summary>
    public ItemDatabase(
        in ILogger<ItemDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs
    ) : base(logger, fileParseService, vfs)
    {
        Load();
    }

    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".toml");

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => new("items/");

    /// <inheritdoc/>
    protected override IEnumerable<ItemDefinition> GetAssetInfo(PathInfo path, ItemDefinitions resource) => resource.Items;

    /// <inheritdoc/>
    protected override string GetAssetID(ItemDefinition assetInfo) => assetInfo.ID;

    /// <inheritdoc/>
    protected override Result<Item> LoadAsset(string id, ItemDefinition assetInfo)
    {
        var item = new Item(
            id,
            assetInfo.Name,
            assetInfo.Icon,
            assetInfo.MaxStack ?? 1,
            assetInfo.Placeable,
            assetInfo.Tool,
            assetInfo.ViewModel,
            assetInfo.WorldModel
        );

        return Result<Item>.FromSuccess(item);
    }
}
