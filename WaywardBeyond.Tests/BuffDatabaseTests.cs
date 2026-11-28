#nullable enable
using System;
using System.IO;
using System.Linq;
using DryIoc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Buffs;
using WaywardBeyond.Data.Statistics;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Covers TOML parsing, validation failures, modifier/regen rules, and DI wiring for <see cref="BuffDefinition"/>.</summary>
public sealed class BuffDatabaseTests
{
    [Fact]
    public void LoadsFlatAndTableArrayBuffsWithModifiersAndRegenRates()
    {
        string tempRoot = CreateTempAssetsDir();
        try
        {
            string buffsDir = Path.Combine(tempRoot, "buffs");
            Directory.CreateDirectory(buffsDir);

            File.WriteAllText(
                Path.Combine(buffsDir, "well_fed.toml"),
                """
                id = "wb:well_fed"
                name = "buff.name.well_fed"
                description = "buff.desc.well_fed"
                icon = "ui/buffs/well_fed.png"
                duration_seconds = 180
                stack_policy = "refresh"

                [regen]
                health_per_sec = 0.5
                oxygen_per_sec = 0.25

                [[stat_modifiers]]
                stat_id = "wb:health_max"
                flat_delta = 20

                [[stat_modifiers]]
                stat_id = "wb:attr.constitution"
                percent_delta = 0.1
                """
            );

            File.WriteAllText(
                Path.Combine(buffsDir, "more_buffs.toml"),
                """
                [[buffs]]
                ID = "wb:oxygen_boost"
                Name = "buff.name.oxygen_boost"
                Icon = "ui/buffs/oxygen_boost.png"
                DurationSeconds = 60.5
                StackPolicy = "extend"
                HealthPerSec = 0
                OxygenPerSec = 2.5

                [[buffs.stat_modifiers]]
                stat_id = "wb:oxygen_max"
                flat_delta = 30

                [[buffs]]
                id = "wb:shielded"
                name = "buff.name.shielded"
                duration_seconds = 15
                stack_policy = "ignore"
                """
            );

            BuffDatabase database = LoadDatabase(tempRoot);

            Assert.Equal(["wb:oxygen_boost", "wb:shielded", "wb:well_fed"], database.All.Select(b => b.ID).ToArray());

            Result<BuffDefinition> wellFedResult = database.Get("wb:well_fed");
            Assert.True(wellFedResult.Success);
            BuffDefinition wellFed = wellFedResult.Value;
            Assert.Equal("buff.name.well_fed", wellFed.Name);
            Assert.Equal("buff.desc.well_fed", wellFed.Description);
            Assert.Equal("ui/buffs/well_fed.png", wellFed.Icon);
            Assert.Equal(180f, wellFed.DurationSeconds);
            Assert.Equal(BuffStackPolicy.Refresh, wellFed.StackPolicy);
            Assert.Equal(0.5f, wellFed.HealthPerSec);
            Assert.Equal(0.25f, wellFed.OxygenPerSec);
            Assert.Equal(StatisticIds.BuffExpiresTick("wb:well_fed"), wellFed.ExpiryStatId);
            Assert.Equal(2, wellFed.StatModifiers.Count);
            Assert.False(wellFed.StatModifiers is BuffStatModifier[]);
            Assert.Equal(new BuffStatModifier("wb:health_max", FlatDelta: 20f), wellFed.StatModifiers[0]);
            Assert.Equal(new BuffStatModifier("wb:attr.constitution", PercentDelta: 0.1f), wellFed.StatModifiers[1]);

            BuffDefinition oxygenBoost = database.Get("wb:oxygen_boost").Value;
            Assert.Equal(BuffStackPolicy.Extend, oxygenBoost.StackPolicy);
            Assert.Equal(60.5f, oxygenBoost.DurationSeconds);
            Assert.Equal(2.5f, oxygenBoost.OxygenPerSec);
            Assert.Single(oxygenBoost.StatModifiers);

            BuffDefinition shielded = database.Get("wb:shielded").Value;
            Assert.Equal(BuffStackPolicy.Ignore, shielded.StackPolicy);
            Assert.Empty(shielded.StatModifiers);
            Assert.False(shielded.StatModifiers is BuffStatModifier[]);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void RejectsInvalidAndDuplicateBuffDefinitionsWhileKeepingValidRows()
    {
        string tempRoot = CreateTempAssetsDir();
        try
        {
            string buffsDir = Path.Combine(tempRoot, "buffs");
            Directory.CreateDirectory(buffsDir);

            File.WriteAllText(
                Path.Combine(buffsDir, "mixed.toml"),
                """
                [[buffs]]
                id = "wb:valid"
                name = "buff.name.valid"
                duration_seconds = 30
                stack_policy = "refresh"

                [[buffs]]
                name = "buff.name.no_id"
                duration_seconds = 30

                [[buffs]]
                id = "wb:no_name"
                duration_seconds = 30

                [[buffs]]
                id = "wb:non_positive_duration"
                name = "buff.name.non_positive"
                duration_seconds = 0

                [[buffs]]
                id = "wb:bad_policy"
                name = "buff.name.bad_policy"
                duration_seconds = 10
                stack_policy = "stack_infinite"

                [[buffs]]
                id = "wb:mod_missing_stat"
                name = "buff.name.mod_missing_stat"
                duration_seconds = 10
                [[buffs.stat_modifiers]]
                flat_delta = 5

                [[buffs]]
                id = "wb:mod_missing_delta"
                name = "buff.name.mod_missing_delta"
                duration_seconds = 10
                [[buffs.stat_modifiers]]
                stat_id = "wb:health_max"

                [[buffs]]
                id = "wb:mod_zero_delta"
                name = "buff.name.mod_zero_delta"
                duration_seconds = 10
                [[buffs.stat_modifiers]]
                stat_id = "wb:health_max"
                flat_delta = 0

                [[buffs]]
                id = "wb:conflicting_regen"
                name = "buff.name.conflicting_regen"
                duration_seconds = 10
                health_per_sec = 1.0
                [buffs.regen]
                health_per_sec = 2.0

                [[buffs]]
                id = "wb:both_regen_tables"
                name = "buff.name.both_regen_tables"
                duration_seconds = 10
                [buffs.regen]
                health_per_sec = 1.0
                [buffs.regen_rates]
                oxygen_per_sec = 2.0

                [[buffs]]
                id = "wb:wrong_duration_type"
                name = "buff.name.wrong_type"
                duration_seconds = "forever"

                [[buffs]]
                id = "wb:duplicate_alias"
                ID = "wb:duplicate_alias_other"
                name = "buff.name.duplicate_alias"
                duration_seconds = 10

                [[buffs]]
                id = "wb:valid"
                name = "buff.name.duplicate"
                duration_seconds = 99
                """
            );

            File.WriteAllText(
                Path.Combine(buffsDir, "mixed_flat_and_array.toml"),
                """
                id = "wb:flat_stray"
                [[buffs]]
                id = "wb:mixed_array_item"
                name = "buff.name.mixed"
                duration_seconds = 10
                """
            );

            File.WriteAllText(Path.Combine(buffsDir, "corrupt.toml"), "id = [broken");

            BuffDatabase database = LoadDatabase(tempRoot);

            BuffDefinition loaded = Assert.Single(database.All);
            Assert.Equal("wb:valid", loaded.ID);
            Assert.Equal(30f, loaded.DurationSeconds);
            Assert.False(database.Get("wb:no_name").Success);
            Assert.False(database.Get("wb:non_positive_duration").Success);
            Assert.False(database.Get("wb:bad_policy").Success);
            Assert.False(database.Get("wb:mod_missing_stat").Success);
            Assert.False(database.Get("wb:mod_missing_delta").Success);
            Assert.False(database.Get("wb:mod_zero_delta").Success);
            Assert.False(database.Get("wb:conflicting_regen").Success);
            Assert.False(database.Get("wb:both_regen_tables").Success);
            Assert.False(database.Get("wb:wrong_duration_type").Success);
            Assert.False(database.Get("wb:duplicate_alias").Success);
            Assert.False(database.Get("wb:mixed_array_item").Success);
            Assert.False(database.All is System.Collections.Generic.List<BuffDefinition>);
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
                id = "wb:well_fed"
                name = "buff.name.well_fed"
                duration_seconds = 120
                stack_policy = "refresh"
                """
            );

            var vfs = new VirtualFileSystem();
            Assert.True(vfs.Mount(new PathInfo(tempRoot)).Success);

            using var container = new Container();
            container.RegisterInstance(vfs);
            container.RegisterInstance<ILogger<BuffDatabase>>(NullLogger<BuffDatabase>.Instance);
            container.RegisterInstance<ILogger<Data.HudBars.HudBarDatabase>>(NullLogger<Data.HudBars.HudBarDatabase>.Instance);
            container.RegisterInstance<ILogger<Data.Localization.LocalizedTagsDatabase>>(NullLogger<Data.Localization.LocalizedTagsDatabase>.Instance);
            container.RegisterInstance<ILogger<Data.Blueprints.BundledBlueprintDatabase>>(NullLogger<Data.Blueprints.BundledBlueprintDatabase>.Instance);
            container.Register<IFileParseService, VirtualFileParseService>(Reuse.Singleton);
            container.RegisterDataServices();
            container.RegisterDataServices();

            IAssetDatabase<BuffDefinition> assetDb = container.Resolve<IAssetDatabase<BuffDefinition>>();
            IBuffDatabase typedDb = container.Resolve<IBuffDatabase>();

            Assert.Same(assetDb, typedDb);
            Assert.True(assetDb.Get("wb:well_fed").Success);
            Assert.Single(typedDb.All);
            Assert.Single(container.ResolveMany<IFileParser>(), p => p is BuffDefinitionsParser);
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
        var parser = new VirtualFileParseService([new BuffDefinitionsParser()], vfs);
        return new BuffDatabase(NullLogger<BuffDatabase>.Instance, parser, vfs);
    }

    private static string CreateTempAssetsDir()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wb-buffs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
