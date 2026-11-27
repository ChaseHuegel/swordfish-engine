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
    private readonly ILogger<ItemDatabase> _logger;

    /// <summary>Constructs the item database from the module's item assets.</summary>
    public ItemDatabase(
        in ILogger<ItemDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs
    ) : base(logger, fileParseService, vfs)
    {
        _logger = logger;
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
        ToolDefinition? tool = ResolveTool(id, assetInfo.Tool);
        var item = new Item(
            id,
            assetInfo.Name,
            assetInfo.Icon,
            assetInfo.MaxStack ?? 1,
            assetInfo.Placeable,
            tool,
            assetInfo.ViewModel,
            assetInfo.WorldModel,
            assetInfo.Description
        );

        return Result<Item>.FromSuccess(item);
    }

    private ToolDefinition? ResolveTool(string id, ToolDefinition? definition)
    {
        if (!definition.HasValue)
        {
            return null;
        }

        ToolDefinition resolved = definition.Value;
        if (resolved.MiningSpeed.HasValue && (!float.IsFinite(resolved.MiningSpeed.Value) || resolved.MiningSpeed.Value <= 0f))
        {
            _logger.LogWarning(
                "Item \"{ItemId}\" tool has invalid MiningSpeed {MiningSpeed}. Using {Default}.",
                id,
                resolved.MiningSpeed.Value,
                ToolDefinition.DEFAULT_MINING_SPEED);
            resolved.MiningSpeed = ToolDefinition.DEFAULT_MINING_SPEED;
        }

        return resolved;
    }
}
