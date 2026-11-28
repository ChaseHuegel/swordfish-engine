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
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Bars;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Covers TOML parsing, validation failures, ordering, and DI wiring for <see cref="Bar"/>.</summary>
public sealed class BarDatabaseTests
{
    [Fact]
    public void LoadsBarsInOrderAndParsesColorTint()
    {
        string tempRoot = CreateTempAssetsDir();
        try
        {
            string barsDir = Path.Combine(tempRoot, "bars");
            Directory.CreateDirectory(barsDir);

            File.WriteAllText(
                Path.Combine(barsDir, "oxygen.toml"),
                """
                [[Bars]]
                ID = "wb:oxygen"
                StatId = "wb:oxygen"
                MaxStatId = "wb:oxygen_max"
                IconCount = 10
                IconFull = "ui/hud/bubble_full.png"
                IconHalf = "ui/hud/bubble_half.png"
                IconEmpty = "ui/hud/bubble_empty.png"
                WarnThreshold = 0.35
                CriticalThreshold = 0.15
                Order = 2
                HideInCreative = true
                ColorTint = "#38B764"
                """
            );

            File.WriteAllText(
                Path.Combine(barsDir, "extras.toml"),
                """
                [[Bars]]
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

                [[Bars]]
                ID = "mod:shield"
                StatId = "mod:shield"
                DefaultMax = 50
                IconCount = 5
                IconFull = "mod/shield_full.png"
                IconHalf = "mod/shield_half.png"
                IconEmpty = "mod/shield_empty.png"
                Order = 3
                HideInCreative = false
                """
            );

            BarDatabase database = LoadDatabase(tempRoot);

            Assert.Equal(["wb:health", "wb:oxygen", "mod:shield"], database.All.Select(b => b.ID).ToArray());

            Result<Bar> oxygenResult = database.Get("wb:oxygen");
            Assert.True(oxygenResult.Success);
            Bar oxygen = oxygenResult.Value;
            Assert.Equal("wb:oxygen", oxygen.StatId);
            Assert.Equal("wb:oxygen_max", oxygen.MaxStatId);
            Assert.Equal(10, oxygen.IconCount);
            Assert.Equal(0.35f, oxygen.WarnThreshold);
            Assert.Equal(0.15f, oxygen.CriticalThreshold);
            Assert.True(oxygen.HideInCreative);
            Assert.Equal("#38B764", oxygen.ColorTint);
            Assert.True(oxygen.ColorTintRgba.HasValue);
            Assert.Equal(new Vector4(0x38 / 255f, 0xB7 / 255f, 0x64 / 255f, 1f), oxygen.ColorTintRgba!.Value);

            Result<Bar> shieldResult = database.Get("mod:shield");
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
    public void RejectsInvalidAndDuplicateBarsWhileKeepingValidRows()
    {
        string tempRoot = CreateTempAssetsDir();
        try
        {
            string barsDir = Path.Combine(tempRoot, "bars");
            Directory.CreateDirectory(barsDir);

            File.WriteAllText(
                Path.Combine(barsDir, "mixed.toml"),
                """
                [[Bars]]
                ID = "wb:valid"
                StatId = "wb:health"
                MaxStatId = "wb:health_max"
                IconCount = 10
                IconFull = "full.png"
                IconHalf = "half.png"
                IconEmpty = "empty.png"
                WarnThreshold = 0.4
                CriticalThreshold = 0.2

                [[Bars]]
                StatId = "wb:health"
                DefaultMax = 100
                IconFull = "full.png"
                IconHalf = "half.png"
                IconEmpty = "empty.png"

                [[Bars]]
                ID = "wb:missing_stat"
                DefaultMax = 100
                IconFull = "full.png"
                IconHalf = "half.png"
                IconEmpty = "empty.png"

                [[Bars]]
                ID = "wb:missing_max"
                StatId = "wb:health"
                IconFull = "full.png"
                IconHalf = "half.png"
                IconEmpty = "empty.png"

                [[Bars]]
                ID = "wb:non_positive_default_max"
                StatId = "wb:health"
                DefaultMax = 0
                IconFull = "full.png"
                IconHalf = "half.png"
                IconEmpty = "empty.png"

                [[Bars]]
                ID = "wb:bad_icon_count"
                StatId = "wb:health"
                DefaultMax = 100
                IconCount = 0
                IconFull = "full.png"
                IconHalf = "half.png"
                IconEmpty = "empty.png"

                [[Bars]]
                ID = "wb:missing_half_icon"
                StatId = "wb:health"
                DefaultMax = 100
                IconFull = "full.png"
                IconEmpty = "empty.png"

                [[Bars]]
                ID = "wb:bad_threshold_range"
                StatId = "wb:health"
                DefaultMax = 100
                IconFull = "full.png"
                IconHalf = "half.png"
                IconEmpty = "empty.png"
                WarnThreshold = 1.2
                CriticalThreshold = 0.2

                [[Bars]]
                ID = "wb:inverted_thresholds"
                StatId = "wb:health"
                DefaultMax = 100
                IconFull = "full.png"
                IconHalf = "half.png"
                IconEmpty = "empty.png"
                WarnThreshold = 0.2
                CriticalThreshold = 0.5

                [[Bars]]
                ID = "wb:bad_color"
                StatId = "wb:health"
                DefaultMax = 100
                IconFull = "full.png"
                IconHalf = "half.png"
                IconEmpty = "empty.png"
                ColorTint = "not-a-hex"

                [[Bars]]
                ID = "wb:blank_color"
                StatId = "wb:health"
                DefaultMax = 100
                IconFull = "full.png"
                IconHalf = "half.png"
                IconEmpty = "empty.png"
                ColorTint = "   "

                [[Bars]]
                ID = "wb:valid"
                StatId = "wb:other"
                DefaultMax = 50
                IconFull = "other_full.png"
                IconHalf = "other_half.png"
                IconEmpty = "other_empty.png"
                """
            );

            File.WriteAllText(
                Path.Combine(barsDir, "wrong_type.toml"),
                """
                [[Bars]]
                ID = "wb:wrong_type"
                StatId = "wb:health"
                DefaultMax = 100
                IconCount = "ten"
                IconFull = "full.png"
                IconHalf = "half.png"
                IconEmpty = "empty.png"
                """
            );

            File.WriteAllText(Path.Combine(barsDir, "corrupt.toml"), "ID = [broken");

            BarDatabase database = LoadDatabase(tempRoot);

            Bar loaded = Assert.Single(database.All);
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
            Assert.False(database.Get("wb:blank_color").Success);
            Assert.False(database.Get("wb:wrong_type").Success);
            Assert.False(database.All is System.Collections.Generic.List<Bar>);
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
            string barsDir = Path.Combine(tempRoot, "bars");
            Directory.CreateDirectory(barsDir);
            File.WriteAllText(
                Path.Combine(barsDir, "health.toml"),
                """
                [[Bars]]
                ID = "wb:health"
                StatId = "wb:health"
                MaxStatId = "wb:health_max"
                IconFull = "full.png"
                IconHalf = "half.png"
                IconEmpty = "empty.png"
                """
            );

            var vfs = new VirtualFileSystem();
            Assert.True(vfs.Mount(new PathInfo(tempRoot)).Success);

            using var container = new Container();
            container.RegisterInstance(vfs);
            container.RegisterInstance<ILogger<BarDatabase>>(NullLogger<BarDatabase>.Instance);
            container.RegisterInstance<ILogger<Data.Buffs.BuffDatabase>>(NullLogger<Data.Buffs.BuffDatabase>.Instance);
            container.RegisterInstance<ILogger<Data.Localization.LocalizedTagsDatabase>>(NullLogger<Data.Localization.LocalizedTagsDatabase>.Instance);
            container.RegisterInstance<ILogger<Data.Blueprints.BundledBlueprintDatabase>>(NullLogger<Data.Blueprints.BundledBlueprintDatabase>.Instance);
            container.Register<IFileParseService, VirtualFileParseService>(Reuse.Singleton);
            container.RegisterDataServices();
            container.RegisterDataServices();

            IAssetDatabase<Bar> assetDb = container.Resolve<IAssetDatabase<Bar>>();
            IBarDatabase typedDb = container.Resolve<IBarDatabase>();

            Assert.Same(assetDb, typedDb);
            Assert.True(assetDb.Get("wb:health").Success);
            Assert.Single(typedDb.All);
            Assert.Single(container.ResolveMany<IFileParser>(), p => p is TomlParser<BarDefinitions>);
            Assert.Single(container.ResolveMany<Shoal.DependencyInjection.IAutoActivate>(), a => a is BarDatabase);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void DataCompositionSkipsBarDatabaseRegistrationWithoutVfs()
    {
        using var container = new Container();
        container.RegisterBars();

        Assert.False(container.IsRegistered<IBarDatabase>());
        Assert.Empty(container.ResolveMany<Shoal.DependencyInjection.IAutoActivate>());
    }

    private static BarDatabase LoadDatabase(string assetsRoot)
    {
        var vfs = new VirtualFileSystem();
        Assert.True(vfs.Mount(new PathInfo(assetsRoot)).Success);
        var parser = new VirtualFileParseService([new TomlParser<BarDefinitions>()], vfs);
        return new BarDatabase(NullLogger<BarDatabase>.Instance, parser, vfs);
    }

    private static string CreateTempAssetsDir()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wb-bars-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
