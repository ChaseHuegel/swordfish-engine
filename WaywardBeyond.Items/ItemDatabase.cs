using System;
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
        IReadOnlyDictionary<string, float>? statistics = ResolveStatistics(id, assetInfo.Statistics);
        var item = new Item(
            id,
            assetInfo.Name,
            assetInfo.Icon,
            assetInfo.MaxStack ?? 1,
            assetInfo.Placeable,
            assetInfo.Tool,
            assetInfo.ViewModel,
            assetInfo.WorldModel,
            assetInfo.Description,
            statistics
        );

        return Result<Item>.FromSuccess(item);
    }

    private IReadOnlyDictionary<string, float>? ResolveStatistics(string id, Dictionary<string, float>? statistics)
    {
        if (statistics == null || statistics.Count == 0)
        {
            return null;
        }

        Dictionary<string, float>? resolved = null;
        foreach (KeyValuePair<string, float> entry in statistics)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                _logger.LogWarning("Item \"{ItemId}\" declares a statistic with an empty key. Skipping.", id);
                continue;
            }

            if (!float.IsFinite(entry.Value))
            {
                _logger.LogWarning(
                    "Item \"{ItemId}\" statistic \"{StatisticId}\" has non-finite value {Value}. Skipping.",
                    id,
                    entry.Key,
                    entry.Value);
                continue;
            }

            resolved ??= new Dictionary<string, float>(statistics.Count, StringComparer.Ordinal);
            resolved[entry.Key] = entry.Value;
        }

        return resolved;
    }
}
