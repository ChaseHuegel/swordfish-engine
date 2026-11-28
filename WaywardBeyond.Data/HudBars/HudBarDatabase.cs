using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace WaywardBeyond.Data.HudBars;

/// <inheritdoc/>
internal sealed class HudBarDatabase : VirtualAssetDatabase<HudBarDefinitions, HudBarDefinition, HudBar>, IAutoActivate, IHudBarDatabase
{
    private readonly List<HudBar> _allBars = [];
    private readonly ReadOnlyCollection<HudBar> _readOnlyBars;

    /// <summary>Constructs and loads the HUD bar database from <c>hud_bars/*.toml</c>.</summary>
    public HudBarDatabase(
        in ILogger<HudBarDatabase> logger,
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
    public IReadOnlyList<HudBar> All => _readOnlyBars;

    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".toml");

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => new("hud_bars/");

    /// <inheritdoc/>
    protected override IEnumerable<HudBarDefinition> GetAssetInfo(PathInfo path, HudBarDefinitions resource)
    {
        if (resource.HudBars != null)
        {
            for (var i = 0; i < resource.HudBars.Count; i++)
            {
                yield return resource.HudBars[i];
            }
        }
    }

    /// <inheritdoc/>
    protected override string GetAssetID(HudBarDefinition assetInfo) => assetInfo.ID?.Trim() ?? string.Empty;

    /// <inheritdoc/>
    protected override Result<HudBar> LoadAsset(string id, HudBarDefinition assetInfo)
    {
        var candidate = new HudBar
        {
            ID = assetInfo.ID ?? string.Empty,
            StatId = assetInfo.StatId ?? string.Empty,
            MaxStatId = assetInfo.MaxStatId,
            DefaultMax = assetInfo.DefaultMax,
            IconCount = assetInfo.IconCount ?? HudBar.DEFAULT_ICON_COUNT,
            IconFull = assetInfo.IconFull ?? string.Empty,
            IconHalf = assetInfo.IconHalf ?? string.Empty,
            IconEmpty = assetInfo.IconEmpty ?? string.Empty,
            WarnThreshold = assetInfo.WarnThreshold ?? HudBar.DEFAULT_WARN_THRESHOLD,
            CriticalThreshold = assetInfo.CriticalThreshold ?? HudBar.DEFAULT_CRITICAL_THRESHOLD,
            Order = assetInfo.Order,
            HideInCreative = assetInfo.HideInCreative,
            ColorTint = assetInfo.ColorTint,
        };

        Result<HudBar> validation = candidate.Validate();
        if (!validation.Success)
        {
            return validation;
        }

        HudBar bar = validation.Value;
        if (Get(bar.ID).Success)
        {
            return Result<HudBar>.FromFailure($"HudBar ID \"{bar.ID}\" is declared more than once.");
        }

        _allBars.Add(bar);
        return Result<HudBar>.FromSuccess(bar);
    }
}
