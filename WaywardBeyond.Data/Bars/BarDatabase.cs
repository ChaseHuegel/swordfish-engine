using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace WaywardBeyond.Data.Bars;

/// <inheritdoc/>
internal sealed class BarDatabase : VirtualAssetDatabase<BarDefinitions, BarDefinition, Bar>, IAutoActivate, IBarDatabase
{
    private readonly List<Bar> _allBars = [];
    private readonly ReadOnlyCollection<Bar> _readOnlyBars;

    /// <summary>Constructs and loads the bar database from <c>bars/*.toml</c>.</summary>
    public BarDatabase(
        in ILogger<BarDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs
    ) : base(logger, fileParseService, vfs)
    {
        _readOnlyBars = _allBars.AsReadOnly();
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
    public IReadOnlyList<Bar> All => _readOnlyBars;

    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".toml");

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => new("bars/");

    /// <inheritdoc/>
    protected override IEnumerable<BarDefinition> GetAssetInfo(PathInfo path, BarDefinitions resource)
    {
        if (resource.Bars != null)
        {
            for (var i = 0; i < resource.Bars.Count; i++)
            {
                yield return resource.Bars[i];
            }
        }
    }

    /// <inheritdoc/>
    protected override string GetAssetID(BarDefinition assetInfo) => assetInfo.ID?.Trim() ?? string.Empty;

    /// <inheritdoc/>
    protected override Result<Bar> LoadAsset(string id, BarDefinition assetInfo)
    {
        var candidate = new Bar
        {
            ID = assetInfo.ID ?? string.Empty,
            StatId = assetInfo.StatId ?? string.Empty,
            MaxStatId = assetInfo.MaxStatId,
            DefaultMax = assetInfo.DefaultMax,
            IconCount = assetInfo.IconCount ?? Bar.DEFAULT_ICON_COUNT,
            IconFull = assetInfo.IconFull ?? string.Empty,
            IconHalf = assetInfo.IconHalf ?? string.Empty,
            IconEmpty = assetInfo.IconEmpty ?? string.Empty,
            WarnThreshold = assetInfo.WarnThreshold ?? Bar.DEFAULT_WARN_THRESHOLD,
            CriticalThreshold = assetInfo.CriticalThreshold ?? Bar.DEFAULT_CRITICAL_THRESHOLD,
            Order = assetInfo.Order,
            HideInCreative = assetInfo.HideInCreative,
            ColorTint = assetInfo.ColorTint,
        };

        Result<Bar> validation = candidate.Validate();
        if (!validation.Success)
        {
            return validation;
        }

        Bar bar = validation.Value;
        if (Get(bar.ID).Success)
        {
            return Result<Bar>.FromFailure($"Bar ID \"{bar.ID}\" is declared more than once.");
        }

        _allBars.Add(bar);
        return Result<Bar>.FromSuccess(bar);
    }
}
