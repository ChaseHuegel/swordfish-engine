#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using DryIoc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Data.HudBars;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Covers TOML parsing, validation failures, ordering, and DI wiring for <see cref="HudBarDefinition"/>.</summary>
public sealed class HudBarDatabaseTests
{
    [Fact]
    public void LoadsFlatAndTableArrayHudBarsInOrderAndParsesColorTint()
    {
        string tempRoot = CreateTempAssetsDir();
        try
        {
            string barsDir = Path.Combine(tempRoot, "hud_bars");
            Directory.CreateDirectory(barsDir);

            File.WriteAllText(
                Path.Combine(barsDir, "oxygen.toml"),
                """
                id = "wb:oxygen"
                stat_id = "wb:oxygen"
                max_stat_id = "wb:oxygen_max"
                icon_count = 10
                icon_full = "ui/hud/bubble_full.png"
                icon_half = "ui/hud/bubble_half.png"
                icon_empty = "ui/hud/bubble_empty.png"
                warn_threshold = 0.35
                critical_threshold = 0.15
                order = 2
                hide_in_creative = true
                color_tint = "#38B764"
                """
            );

            File.WriteAllText(
                Path.Combine(barsDir, "extras.toml"),
                """
                [[hud_bars]]
                ID = "wb:health"
                StatId = "wb:health"
                MaxStatId = "wb:health_max"
                IconCount = 10
                IconFull = "ui/hud/heart_full.png"
                IconHalf = "ui/hud/heart_half.png"
                IconEmpty = "ui/hud/heart_empty.png"
                WarnThreshold = 0.3
                CriticalThreshold = 0.1
                Order = 1
                HideInCreative = true

                [[hud_bars]]
                id = "mod:shield"
                stat_id = "mod:shield"
                default_max = 50
                icon_count = 5
                icon_full = "mod/shield_full.png"
                icon_half = "mod/shield_half.png"
                icon_empty = "mod/shield_empty.png"
                order = 3
                hide_in_creative = false
                """
            );

            HudBarDatabase database = LoadDatabase(tempRoot);

            Assert.Equal(["wb:health", "wb:oxygen", "mod:shield"], database.All.Select(b => b.ID).ToArray());

            Result<HudBarDefinition> oxygenResult = database.Get("wb:oxygen");
            Assert.True(oxygenResult.Success);
            HudBarDefinition oxygen = oxygenResult.Value;
            Assert.Equal("wb:oxygen", oxygen.StatId);
            Assert.Equal("wb:oxygen_max", oxygen.MaxStatId);
            Assert.Equal(10, oxygen.IconCount);
            Assert.Equal(0.35f, oxygen.WarnThreshold);
            Assert.Equal(0.15f, oxygen.CriticalThreshold);
            Assert.True(oxygen.HideInCreative);
            Assert.Equal("#38B764", oxygen.ColorTint);
            Assert.True(oxygen.ColorTintRgba.HasValue);
            Assert.Equal(new Vector4(0x38 / 255f, 0xB7 / 255f, 0x64 / 255f, 1f), oxygen.ColorTintRgba!.Value);

            Result<HudBarDefinition> shieldResult = database.Get("mod:shield");
            Assert.True(shieldResult.Success);
            Assert.Null(shieldResult.Value.MaxStatId);
            Assert.Equal(50f, shieldResult.Value.DefaultMax);
            Assert.Equal(5, shieldResult.Value.IconCount);
            Assert.False(shieldResult.Value.HideInCreative);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void RejectsInvalidAndDuplicateHudBarsWhileKeepingValidRows()
    {
        string tempRoot = CreateTempAssetsDir();
        try
        {
            string barsDir = Path.Combine(tempRoot, "hud_bars");
            Directory.CreateDirectory(barsDir);

            File.WriteAllText(
                Path.Combine(barsDir, "mixed.toml"),
                """
                [[hud_bars]]
                id = "wb:valid"
                stat_id = "wb:health"
                max_stat_id = "wb:health_max"
                icon_count = 10
                icon_full = "full.png"
                icon_half = "half.png"
                icon_empty = "empty.png"
                warn_threshold = 0.4
                critical_threshold = 0.2

                [[hud_bars]]
                stat_id = "wb:health"
                default_max = 100
                icon_full = "full.png"
                icon_half = "half.png"
                icon_empty = "empty.png"

                [[hud_bars]]
                id = "wb:missing_stat"
                default_max = 100
                icon_full = "full.png"
                icon_half = "half.png"
                icon_empty = "empty.png"

                [[hud_bars]]
                id = "wb:missing_max"
                stat_id = "wb:health"
                icon_full = "full.png"
                icon_half = "half.png"
                icon_empty = "empty.png"

                [[hud_bars]]
                id = "wb:non_positive_default_max"
                stat_id = "wb:health"
                default_max = 0
                icon_full = "full.png"
                icon_half = "half.png"
                icon_empty = "empty.png"

                [[hud_bars]]
                id = "wb:bad_icon_count"
                stat_id = "wb:health"
                default_max = 100
                icon_count = 0
                icon_full = "full.png"
                icon_half = "half.png"
                icon_empty = "empty.png"

                [[hud_bars]]
                id = "wb:missing_half_icon"
                stat_id = "wb:health"
                default_max = 100
                icon_full = "full.png"
                icon_empty = "empty.png"

                [[hud_bars]]
                id = "wb:bad_threshold_range"
                stat_id = "wb:health"
                default_max = 100
                icon_full = "full.png"
                icon_half = "half.png"
                icon_empty = "empty.png"
                warn_threshold = 1.2
                critical_threshold = 0.2

                [[hud_bars]]
                id = "wb:inverted_thresholds"
                stat_id = "wb:health"
                default_max = 100
                icon_full = "full.png"
                icon_half = "half.png"
                icon_empty = "empty.png"
                warn_threshold = 0.2
                critical_threshold = 0.5

                [[hud_bars]]
                id = "wb:bad_color"
                stat_id = "wb:health"
                default_max = 100
                icon_full = "full.png"
                icon_half = "half.png"
                icon_empty = "empty.png"
                color_tint = "not-a-hex"

                [[hud_bars]]
                id = "wb:wrong_type"
                stat_id = "wb:health"
                default_max = 100
                icon_count = "ten"
                icon_full = "full.png"
                icon_half = "half.png"
                icon_empty = "empty.png"

                [[hud_bars]]
                id = "wb:valid"
                stat_id = "wb:other"
                default_max = 50
                icon_full = "other_full.png"
                icon_half = "other_half.png"
                icon_empty = "other_empty.png"
                """
            );

            File.WriteAllText(Path.Combine(barsDir, "corrupt.toml"), "id = [broken");

            HudBarDatabase database = LoadDatabase(tempRoot);

            HudBarDefinition loaded = Assert.Single(database.All);
            Assert.Equal("wb:valid", loaded.ID);
            Assert.Equal("wb:health", loaded.StatId);
            Assert.False(database.Get("wb:missing_stat").Success);
            Assert.False(database.Get("wb:missing_max").Success);
            Assert.False(database.Get("wb:non_positive_default_max").Success);
            Assert.False(database.Get("wb:bad_icon_count").Success);
            Assert.False(database.Get("wb:missing_half_icon").Success);
            Assert.False(database.Get("wb:bad_threshold_range").Success);
            Assert.False(database.Get("wb:inverted_thresholds").Success);
            Assert.False(database.Get("wb:bad_color").Success);
            Assert.False(database.Get("wb:wrong_type").Success);
            Assert.False(database.All is System.Collections.Generic.List<HudBarDefinition>);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void DataCompositionResolvesIAssetDatabaseAndTypedInterface()
    {
        string tempRoot = CreateTempAssetsDir();
        try
        {
            string barsDir = Path.Combine(tempRoot, "hud_bars");
            Directory.CreateDirectory(barsDir);
            File.WriteAllText(
                Path.Combine(barsDir, "health.toml"),
                """
                id = "wb:health"
                stat_id = "wb:health"
                max_stat_id = "wb:health_max"
                icon_full = "full.png"
                icon_half = "half.png"
                icon_empty = "empty.png"
                """
            );

            var vfs = new VirtualFileSystem();
            Assert.True(vfs.Mount(new PathInfo(tempRoot)).Success);

            using var container = new Container();
            container.RegisterInstance(vfs);
            container.RegisterInstance<ILogger<HudBarDatabase>>(NullLogger<HudBarDatabase>.Instance);
            container.RegisterInstance<ILogger<Data.Buffs.BuffDatabase>>(NullLogger<Data.Buffs.BuffDatabase>.Instance);
            container.RegisterInstance<ILogger<Data.Localization.LocalizedTagsDatabase>>(NullLogger<Data.Localization.LocalizedTagsDatabase>.Instance);
            container.RegisterInstance<ILogger<Data.Blueprints.BundledBlueprintDatabase>>(NullLogger<Data.Blueprints.BundledBlueprintDatabase>.Instance);
            container.Register<IFileParseService, VirtualFileParseService>(Reuse.Singleton);
            container.RegisterDataServices();
            container.RegisterDataServices();

            IAssetDatabase<HudBarDefinition> assetDb = container.Resolve<IAssetDatabase<HudBarDefinition>>();
            IHudBarDatabase typedDb = container.Resolve<IHudBarDatabase>();

            Assert.Same(assetDb, typedDb);
            Assert.True(assetDb.Get("wb:health").Success);
            Assert.Single(typedDb.All);
            Assert.Single(container.ResolveMany<IFileParser>(), p => p is HudBarDefinitionsParser);
            Assert.Single(container.ResolveMany<Shoal.DependencyInjection.IAutoActivate>(), a => a is HudBarDatabase);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void DataCompositionSkipsHudBarDatabaseRegistrationWithoutVfs()
    {
        using var container = new Container();
        container.RegisterHudBars();

        Assert.False(container.IsRegistered<IHudBarDatabase>());
        Assert.Empty(container.ResolveMany<Shoal.DependencyInjection.IAutoActivate>());
    }

    private static HudBarDatabase LoadDatabase(string assetsRoot)
    {
        var vfs = new VirtualFileSystem();
        Assert.True(vfs.Mount(new PathInfo(assetsRoot)).Success);
        var parser = new VirtualFileParseService([new HudBarDefinitionsParser()], vfs);
        return new HudBarDatabase(NullLogger<HudBarDatabase>.Instance, parser, vfs);
    }

    private static string CreateTempAssetsDir()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wb-hudbars-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
