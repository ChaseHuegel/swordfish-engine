using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace WaywardBeyond.Data.HudBars;

/// <inheritdoc/>
internal sealed class HudBarDatabase : VirtualAssetDatabase<HudBarDefinitions, HudBarEntry, HudBarDefinition>, IAutoActivate, IHudBarDatabase
{
    private readonly List<HudBarDefinition> _allBars = [];

    /// <summary>Constructs and loads the HUD bar database from <c>hud_bars/*.toml</c>.</summary>
    public HudBarDatabase(
        in ILogger<HudBarDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs
    ) : base(logger, fileParseService, vfs)
    {
        Load();
        _allBars.Sort(static (left, right) =>
        {
            int orderCompare = left.Order.CompareTo(right.Order);
            return orderCompare != 0
                ? orderCompare
                : StringComparer.Ordinal.Compare(left.ID, right.ID);
        });
    }

    /// <inheritdoc/>
    public IReadOnlyList<HudBarDefinition> All => _allBars;

    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".toml");

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => new("hud_bars/");

    /// <inheritdoc/>
    protected override IEnumerable<HudBarEntry> GetAssetInfo(PathInfo path, HudBarDefinitions resource) => resource.Entries;

    /// <inheritdoc/>
    protected override string GetAssetID(HudBarEntry assetInfo) => assetInfo.Definition.ID?.Trim() ?? string.Empty;

    /// <inheritdoc/>
    protected override Result<HudBarDefinition> LoadAsset(string id, HudBarEntry assetInfo)
    {
        if (assetInfo.Error != null)
        {
            return Result<HudBarDefinition>.FromFailure(assetInfo.Error);
        }

        Result<HudBarDefinition> validation = assetInfo.Definition.Validate();
        if (!validation.Success)
        {
            return validation;
        }

        HudBarDefinition bar = validation.Value;
        if (Get(bar.ID).Success)
        {
            return Result<HudBarDefinition>.FromFailure($"HudBar ID \"{bar.ID}\" is declared more than once.");
        }

        _allBars.Add(bar);
        return Result<HudBarDefinition>.FromSuccess(bar);
    }
}
