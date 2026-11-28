#nullable enable
using System;
using System.IO;
using System.Linq;
using DryIoc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Buffs;
using WaywardBeyond.Data.Statistics;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Covers TOML parsing, validation failures, modifier/regen rules, and DI wiring for <see cref="Buff"/>.</summary>
public sealed class BuffDatabaseTests
{
    [Fact]
    public void LoadsBuffsWithModifiersAndDataDrivenRegenRates()
    {
        string tempRoot = CreateTempAssetsDir();
        try
        {
            string buffsDir = Path.Combine(tempRoot, "buffs");
            Directory.CreateDirectory(buffsDir);

            File.WriteAllText(
                Path.Combine(buffsDir, "well_fed.toml"),
                """
                [[Buffs]]
                ID = "wb:well_fed"
                Name = "buff.name.well_fed"
                Description = "buff.desc.well_fed"
                Icon = "ui/buffs/well_fed.png"
                DurationSeconds = 180
                StackPolicy = "Refresh"

                [[Buffs.StatModifiers]]
                StatId = "wb:health_max"
                FlatDelta = 20

                [[Buffs.StatModifiers]]
                StatId = "wb:resolve"
                PercentDelta = 0.1

                [[Buffs.RegenRates]]
                StatId = "wb:health"
                PerSecond = 0.5

                [[Buffs.RegenRates]]
                StatId = "wb:oxygen"
                PerSecond = 0.25
                """
            );

            File.WriteAllText(
                Path.Combine(buffsDir, "more_buffs.toml"),
                """
                [[Buffs]]
                ID = "wb:oxygen_boost"
                Name = "buff.name.oxygen_boost"
                Icon = "ui/buffs/oxygen_boost.png"
                DurationSeconds = 60.5
                StackPolicy = "Extend"

                [[Buffs.StatModifiers]]
                StatId = "wb:oxygen_max"
                FlatDelta = 30

                [[Buffs.RegenRates]]
                StatId = "mod:shield"
                PerSecond = 2.5

                [[Buffs]]
                ID = "wb:shielded"
                Name = "buff.name.shielded"
                DurationSeconds = 15
                StackPolicy = "Ignore"
                """
            );

            BuffDatabase database = LoadDatabase(tempRoot);

            Assert.Equal(["wb:oxygen_boost", "wb:shielded", "wb:well_fed"], database.All.Select(b => b.ID).ToArray());

            Result<Buff> wellFedResult = database.Get("wb:well_fed");
            Assert.True(wellFedResult.Success);
            Buff wellFed = wellFedResult.Value;
            Assert.Equal("buff.name.well_fed", wellFed.Name);
            Assert.Equal("buff.desc.well_fed", wellFed.Description);
            Assert.Equal("ui/buffs/well_fed.png", wellFed.Icon);
            Assert.Equal(180f, wellFed.DurationSeconds);
            Assert.Equal(BuffStackPolicy.Refresh, wellFed.StackPolicy);
            Assert.Equal(StatisticIds.BuffExpiresTick("wb:well_fed"), wellFed.ExpiryStatId);
            Assert.Equal(2, wellFed.StatModifiers.Count);
            Assert.False(wellFed.StatModifiers is BuffStatModifier[]);
            Assert.Equal(new BuffStatModifier("wb:health_max", FlatDelta: 20f), wellFed.StatModifiers[0]);
            Assert.Equal(new BuffStatModifier("wb:resolve", PercentDelta: 0.1f), wellFed.StatModifiers[1]);
            Assert.Equal(2, wellFed.RegenRates.Count);
            Assert.False(wellFed.RegenRates is BuffRegenRate[]);
            Assert.Equal(new BuffRegenRate("wb:health", 0.5f), wellFed.RegenRates[0]);
            Assert.Equal(new BuffRegenRate("wb:oxygen", 0.25f), wellFed.RegenRates[1]);

            Buff oxygenBoost = database.Get("wb:oxygen_boost").Value;
            Assert.Equal(BuffStackPolicy.Extend, oxygenBoost.StackPolicy);
            Assert.Equal(60.5f, oxygenBoost.DurationSeconds);
            Assert.Single(oxygenBoost.StatModifiers);
            Assert.Equal(new BuffRegenRate("mod:shield", 2.5f), Assert.Single(oxygenBoost.RegenRates));

            Buff shielded = database.Get("wb:shielded").Value;
            Assert.Equal(BuffStackPolicy.Ignore, shielded.StackPolicy);
            Assert.Empty(shielded.StatModifiers);
            Assert.Empty(shielded.RegenRates);
            Assert.False(shielded.StatModifiers is BuffStatModifier[]);
            Assert.False(shielded.RegenRates is BuffRegenRate[]);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void RejectsInvalidAndDuplicateBuffsWhileKeepingValidRows()
    {
        string tempRoot = CreateTempAssetsDir();
        try
        {
            string buffsDir = Path.Combine(tempRoot, "buffs");
            Directory.CreateDirectory(buffsDir);

            File.WriteAllText(
                Path.Combine(buffsDir, "mixed.toml"),
                """
                [[Buffs]]
                ID = "wb:valid"
                Name = "buff.name.valid"
                DurationSeconds = 30
                StackPolicy = "Refresh"

                [[Buffs]]
                Name = "buff.name.no_id"
                DurationSeconds = 30

                [[Buffs]]
                ID = "wb:no_name"
                DurationSeconds = 30

                [[Buffs]]
                ID = "wb:non_positive_duration"
                Name = "buff.name.non_positive"
                DurationSeconds = 0

                [[Buffs]]
                ID = "wb:bad_policy"
                Name = "buff.name.bad_policy"
                DurationSeconds = 10
                StackPolicy = "stack_infinite"

                [[Buffs]]
                ID = "wb:mod_missing_stat"
                Name = "buff.name.mod_missing_stat"
                DurationSeconds = 10
                [[Buffs.StatModifiers]]
                FlatDelta = 5

                [[Buffs]]
                ID = "wb:mod_missing_delta"
                Name = "buff.name.mod_missing_delta"
                DurationSeconds = 10
                [[Buffs.StatModifiers]]
                StatId = "wb:health_max"

                [[Buffs]]
                ID = "wb:mod_zero_delta"
                Name = "buff.name.mod_zero_delta"
                DurationSeconds = 10
                [[Buffs.StatModifiers]]
                StatId = "wb:health_max"
                FlatDelta = 0

                [[Buffs]]
                ID = "wb:regen_missing_stat"
                Name = "buff.name.regen_missing_stat"
                DurationSeconds = 10
                [[Buffs.RegenRates]]
                PerSecond = 1.0

                [[Buffs]]
                ID = "wb:regen_zero_rate"
                Name = "buff.name.regen_zero_rate"
                DurationSeconds = 10
                [[Buffs.RegenRates]]
                StatId = "wb:health"
                PerSecond = 0

                [[Buffs]]
                ID = "wb:valid"
                Name = "buff.name.duplicate"
                DurationSeconds = 99
                """
            );

            File.WriteAllText(
                Path.Combine(buffsDir, "wrong_type.toml"),
                """
                [[Buffs]]
                ID = "wb:wrong_duration_type"
                Name = "buff.name.wrong_type"
                DurationSeconds = "forever"
                """
            );

            File.WriteAllText(Path.Combine(buffsDir, "corrupt.toml"), "ID = [broken");

            BuffDatabase database = LoadDatabase(tempRoot);

            Buff loaded = Assert.Single(database.All);
            Assert.Equal("wb:valid", loaded.ID);
            Assert.Equal(30f, loaded.DurationSeconds);
            Assert.False(database.Get("wb:no_name").Success);
            Assert.False(database.Get("wb:non_positive_duration").Success);
            Assert.False(database.Get("wb:bad_policy").Success);
            Assert.False(database.Get("wb:mod_missing_stat").Success);
            Assert.False(database.Get("wb:mod_missing_delta").Success);
            Assert.False(database.Get("wb:mod_zero_delta").Success);
            Assert.False(database.Get("wb:regen_missing_stat").Success);
            Assert.False(database.Get("wb:regen_zero_rate").Success);
            Assert.False(database.Get("wb:wrong_duration_type").Success);
            Assert.False(database.All is System.Collections.Generic.List<Buff>);
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
            string buffsDir = Path.Combine(tempRoot, "buffs");
            Directory.CreateDirectory(buffsDir);
            File.WriteAllText(
                Path.Combine(buffsDir, "ration.toml"),
                """
                [[Buffs]]
                ID = "wb:well_fed"
                Name = "buff.name.well_fed"
                DurationSeconds = 120
                StackPolicy = "Refresh"
                """
            );

            var vfs = new VirtualFileSystem();
            Assert.True(vfs.Mount(new PathInfo(tempRoot)).Success);

            using var container = new Container();
            container.RegisterInstance(vfs);
            container.RegisterInstance<ILogger<BuffDatabase>>(NullLogger<BuffDatabase>.Instance);
            container.RegisterInstance<ILogger<Data.Bars.BarDatabase>>(NullLogger<Data.Bars.BarDatabase>.Instance);
            container.RegisterInstance<ILogger<Data.Localization.LocalizedTagsDatabase>>(NullLogger<Data.Localization.LocalizedTagsDatabase>.Instance);
            container.RegisterInstance<ILogger<Data.Blueprints.BundledBlueprintDatabase>>(NullLogger<Data.Blueprints.BundledBlueprintDatabase>.Instance);
            container.Register<IFileParseService, VirtualFileParseService>(Reuse.Singleton);
            container.RegisterDataServices();
            container.RegisterDataServices();

            IAssetDatabase<Buff> assetDb = container.Resolve<IAssetDatabase<Buff>>();
            IBuffDatabase typedDb = container.Resolve<IBuffDatabase>();

            Assert.Same(assetDb, typedDb);
            Assert.True(assetDb.Get("wb:well_fed").Success);
            Assert.Single(typedDb.All);
            Assert.Single(container.ResolveMany<IFileParser>(), p => p is TomlParser<BuffDefinitions>);
            Assert.Single(container.ResolveMany<Shoal.DependencyInjection.IAutoActivate>(), a => a is BuffDatabase);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void DataCompositionSkipsBuffDatabaseRegistrationWithoutVfs()
    {
        using var container = new Container();
        container.RegisterBuffs();

        Assert.False(container.IsRegistered<IBuffDatabase>());
        Assert.Empty(container.ResolveMany<Shoal.DependencyInjection.IAutoActivate>());
    }

    private static BuffDatabase LoadDatabase(string assetsRoot)
    {
        var vfs = new VirtualFileSystem();
        Assert.True(vfs.Mount(new PathInfo(assetsRoot)).Success);
        var parser = new VirtualFileParseService([new TomlParser<BuffDefinitions>()], vfs);
        return new BuffDatabase(NullLogger<BuffDatabase>.Instance, parser, vfs);
    }

    private static string CreateTempAssetsDir()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wb-buffs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
