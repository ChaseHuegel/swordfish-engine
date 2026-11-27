using System;
using System.Collections.Generic;
using System.IO;
using DryIoc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Items;
using WaywardBeyond.Recipes;
using WaywardBeyond.Skills;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>
/// Headless coverage for <see cref="RecipeDatabase"/>: TOML parsing, item/tag validation, station
/// ordering, zero-allocation auto-matching, and valid input queries.
/// </summary>
public class RecipeDatabaseTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"wb-recipes-tests-{Guid.NewGuid():N}");

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ParsesValidRecipesWithOptionalFieldsAndGeneratorPower()
    {
        string recipesDir = CreateAssetsDir(
            "valid",
            (
                "recipes/refinery.toml",
                """
                [[recipes]]
                id               = "wb:smelt_panel"
                station_tag      = "station.refinery"
                mode             = "auto"
                duration_seconds = 2.5
                power_kw         = -18.0
                category         = "recipe.category.hull"
                order            = 10

                [[recipes.inputs]]
                item_id    = "wb:rock"
                count      = 2
                slot_group = "wb:input"

                [[recipes.outputs]]
                item_id = "wb:panel"
                count   = 1
                chance  = 0.75

                [[recipes]]
                id               = "wb:burn_ice"
                station_tag      = "station.generator"
                mode             = "Auto"
                duration_seconds = 5
                power_kw         = 60
                order            = 1

                [[recipes.inputs]]
                item_id = "wb:ice"
                count   = 1

                [[Recipes]]
                id               = "wb:hand_grate"
                station_tag      = "station.suit"
                duration_seconds = 0
                power_kw         = 0

                [[Recipes.inputs]]
                item_id = "tag:item.placeable"
                count   = 1

                [[Recipes.outputs]]
                item_id = "wb:grate"
                count   = 2
                """
            )
        );

        RecipeDatabase recipes = LoadRecipes(recipesDir);

        Result<Recipe> smeltResult = recipes.Get("wb:smelt_panel");
        Assert.True(smeltResult.Success, smeltResult.Message);
        Recipe smelt = smeltResult.Value;
        Assert.Equal("station.refinery", smelt.StationTag);
        Assert.Equal(RecipeMode.Auto, smelt.Mode);
        Assert.Equal(2.5f, smelt.DurationSeconds);
        Assert.Equal(-18.0f, smelt.PowerKw);
        Assert.Equal("recipe.category.hull", smelt.Category);
        Assert.Equal(10, smelt.Order);
        Assert.Single(smelt.Inputs);
        Assert.Equal("wb:rock", smelt.Inputs[0].ItemId);
        Assert.Null(smelt.Inputs[0].Tag);
        Assert.False(smelt.Inputs[0].IsTag);
        Assert.Equal(2, smelt.Inputs[0].Count);
        Assert.Equal("wb:input", smelt.Inputs[0].SlotGroup);
        Assert.Single(smelt.Outputs);
        Assert.Equal("wb:panel", smelt.Outputs[0].ItemId);
        Assert.Equal(1, smelt.Outputs[0].Count);
        Assert.Equal(0.75f, smelt.Outputs[0].Chance);

        Result<Recipe> generatorResult = recipes.Get("wb:burn_ice");
        Assert.True(generatorResult.Success, generatorResult.Message);
        Recipe generator = generatorResult.Value;
        Assert.Equal(RecipeMode.Auto, generator.Mode);
        Assert.Equal(60f, generator.PowerKw);
        Assert.Empty(generator.Outputs);

        Result<Recipe> manualResult = recipes.Get("wb:hand_grate");
        Assert.True(manualResult.Success, manualResult.Message);
        Recipe manual = manualResult.Value;
        Assert.Equal(RecipeMode.Manual, manual.Mode);
        Assert.True(manual.Inputs[0].IsTag);
        Assert.Equal("item.placeable", manual.Inputs[0].Tag);
        Assert.Null(manual.Inputs[0].ItemId);
        Assert.True(manual.Inputs[0].Matches("wb:rock"));
        Assert.False(manual.Inputs[0].Matches("laser"));
        Assert.Equal(1f, manual.Outputs[0].Chance);
    }

    [Fact]
    public void RejectsInvalidRecipesAndLogsWithoutCrashing()
    {
        string recipesDir = CreateAssetsDir(
            "invalid",
            (
                "lang/tags/bad_recipe_tags.toml",
                """
                "Language" = ""

                [Tags]
                "recipe.has_missing_item" = [
                    "wb:rock",
                    "wb:does_not_exist",
                ]
                """
            ),
            (
                "recipes/mixed.toml",
                """
                [[recipes]]
                id               = "wb:valid_first"
                station_tag      = "station.refinery"
                mode             = "manual"
                duration_seconds = 1.0
                power_kw         = -5.0
                inputs           = [{ item_id = "wb:rock", count = 1 }]
                outputs          = [{ item_id = "wb:panel", count = 1 }]

                [[recipes]]
                id               = "wb:valid_first"
                station_tag      = "station.refinery"
                mode             = "auto"
                duration_seconds = 2.0
                inputs           = [{ item_id = "wb:rock", count = 2 }]
                outputs          = [{ item_id = "wb:grate", count = 1 }]

                [[recipes]]
                station_tag      = "station.refinery"
                inputs           = [{ item_id = "wb:rock", count = 1 }]
                outputs          = [{ item_id = "wb:panel", count = 1 }]

                [[recipes]]
                id               = "wb:missing_station"
                inputs           = [{ item_id = "wb:rock", count = 1 }]
                outputs          = [{ item_id = "wb:panel", count = 1 }]

                [[recipes]]
                id               = "wb:bad_mode"
                station_tag      = "station.refinery"
                mode             = "turbo"
                inputs           = [{ item_id = "wb:rock", count = 1 }]
                outputs          = [{ item_id = "wb:panel", count = 1 }]

                [[recipes]]
                id               = "wb:negative_duration"
                station_tag      = "station.refinery"
                duration_seconds = -1.0
                inputs           = [{ item_id = "wb:rock", count = 1 }]
                outputs          = [{ item_id = "wb:panel", count = 1 }]

                [[recipes]]
                id               = "wb:unknown_input_item"
                station_tag      = "station.refinery"
                inputs           = [{ item_id = "wb:missing_item", count = 1 }]
                outputs          = [{ item_id = "wb:panel", count = 1 }]

                [[recipes]]
                id               = "wb:unknown_input_tag"
                station_tag      = "station.refinery"
                inputs           = [{ item_id = "tag:missing.tag", count = 1 }]
                outputs          = [{ item_id = "wb:panel", count = 1 }]

                [[recipes]]
                id               = "wb:tag_with_unknown_item"
                station_tag      = "station.refinery"
                inputs           = [{ item_id = "tag:recipe.has_missing_item", count = 1 }]
                outputs          = [{ item_id = "wb:panel", count = 1 }]

                [[recipes]]
                id               = "wb:unknown_output_item"
                station_tag      = "station.refinery"
                inputs           = [{ item_id = "wb:rock", count = 1 }]
                outputs          = [{ item_id = "wb:missing_output", count = 1 }]

                [[recipes]]
                id               = "wb:bad_chance"
                station_tag      = "station.refinery"
                inputs           = [{ item_id = "wb:rock", count = 1 }]
                outputs          = [{ item_id = "wb:panel", count = 1, chance = 1.5 }]

                [[recipes]]
                id               = "wb:no_outputs_or_power"
                station_tag      = "station.refinery"
                inputs           = [{ item_id = "wb:rock", count = 1 }]
                power_kw         = -5.0
                """
            ),
            (
                "recipes/corrupt.toml",
                "[[recipes]\nnot valid toml = = ="
            )
        );

        var logger = new CapturingLogger<RecipeDatabase>();
        RecipeDatabase recipes = LoadRecipes(recipesDir, logger);

        //  Only the valid entry loads, and duplicate ID does not overwrite it.
        Assert.Single(recipes.All);
        Result<Recipe> valid = recipes.Get("wb:valid_first");
        Assert.True(valid.Success);
        Assert.Equal(RecipeMode.Manual, valid.Value.Mode);

        Assert.False(recipes.Get("wb:missing_station").Success);
        Assert.False(recipes.Get("wb:bad_mode").Success);
        Assert.False(recipes.Get("wb:negative_duration").Success);
        Assert.False(recipes.Get("wb:unknown_input_item").Success);
        Assert.False(recipes.Get("wb:unknown_input_tag").Success);
        Assert.False(recipes.Get("wb:tag_with_unknown_item").Success);
        Assert.False(recipes.Get("wb:unknown_output_item").Success);
        Assert.False(recipes.Get("wb:bad_chance").Success);
        Assert.False(recipes.Get("wb:no_outputs_or_power").Success);
        Assert.Contains(logger.Entries, static entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public void GetByStationTagOrdersByOrderThenId()
    {
        string recipesDir = CreateAssetsDir(
            "ordering",
            (
                "recipes/ordered.toml",
                """
                [[recipes]]
                id          = "wb:z_second"
                station_tag = "station.factory"
                order       = 2
                inputs      = [{ item_id = "wb:rock", count = 1 }]
                outputs     = [{ item_id = "wb:panel", count = 1 }]

                [[recipes]]
                id          = "wb:b_tie"
                station_tag = "station.factory"
                order       = 1
                inputs      = [{ item_id = "wb:rock", count = 1 }]
                outputs     = [{ item_id = "wb:panel", count = 1 }]

                [[recipes]]
                id          = "wb:a_tie"
                station_tag = "station.factory"
                order       = 1
                inputs      = [{ item_id = "wb:rock", count = 1 }]
                outputs     = [{ item_id = "wb:panel", count = 1 }]

                [[recipes]]
                id          = "wb:other_station"
                station_tag = "station.oven"
                order       = 0
                inputs      = [{ item_id = "wb:ice", count = 1 }]
                outputs     = [{ item_id = "wb:glass", count = 1 }]
                """
            )
        );

        RecipeDatabase recipes = LoadRecipes(recipesDir);

        IReadOnlyList<Recipe> factory = recipes.GetByStationTag("station.factory");
        Assert.Equal(3, factory.Count);
        Assert.Equal("wb:a_tie", factory[0].ID);
        Assert.Equal("wb:b_tie", factory[1].ID);
        Assert.Equal("wb:z_second", factory[2].ID);

        Assert.Single(recipes.GetByStationTag("station.oven"));
        Assert.Empty(recipes.GetByStationTag("station.unknown"));
    }

    [Fact]
    public void FindAutoMatchAndValidInputChecksHandleStacksTagsAndModes()
    {
        string recipesDir = CreateAssetsDir(
            "automatch",
            (
                "lang/tags/recipe_inputs.toml",
                """
                "Language" = ""

                [Tags]
                "recipe.mineral" = [
                    "wb:rock",
                    "wb:ice",
                ]
                """
            ),
            (
                "recipes/stations.toml",
                """
                [[recipes]]
                id          = "wb:manual_only"
                station_tag = "station.refinery"
                mode        = "manual"
                order       = 0
                inputs      = [{ item_id = "wb:rock", count = 1 }]
                outputs     = [{ item_id = "wb:panel", count = 1 }]

                [[recipes]]
                id          = "wb:refined_combo"
                station_tag = "station.refinery"
                mode        = "auto"
                order       = 1
                inputs      = [
                    { item_id = "wb:rock", count = 2 },
                    { item_id = "tag:recipe.mineral", count = 1 },
                ]
                outputs     = [{ item_id = "wb:core", count = 1 }]

                [[recipes]]
                id          = "wb:single_mineral"
                station_tag = "station.refinery"
                mode        = "auto"
                order       = 2
                inputs      = [{ item_id = "tag:recipe.mineral", count = 2 }]
                outputs     = [{ item_id = "wb:glass", count = 1 }]
                """
            )
        );

        IRecipeDatabase recipes = LoadRecipes(recipesDir);

        //  Valid input check covers both direct and tag-expanded ingredients across auto and manual modes.
        Assert.True(recipes.IsValidInputForStation("station.refinery", "wb:rock"));
        Assert.True(recipes.IsValidInputForStation("station.refinery", "wb:ice"));
        Assert.False(recipes.IsValidInputForStation("station.refinery", "wb:panel"));
        Assert.False(recipes.IsValidInputForStation("station.unknown", "wb:rock"));

        //  Manual recipe is ignored by FindAutoMatch, and insufficient count returns failure.
        var oneRock = new InventoryComponent(4);
        oneRock.Contents[0] = new ItemData("wb:rock", 1, 100);
        Assert.False(recipes.FindAutoMatch("station.refinery", in oneRock).Success);
        Assert.False(recipes.FindAutoMatch("station.refinery", oneRock.Contents.AsSpan()).Success);

        //  2 rocks across split slots satisfy wb:single_mineral (but not wb:refined_combo which needs 3 total).
        var twoRocks = new InventoryComponent(4);
        twoRocks.Contents[0] = new ItemData("wb:rock", 1, 100);
        twoRocks.Contents[2] = new ItemData("wb:rock", 1, 100);
        Result<Recipe> matchSplit = recipes.FindAutoMatch("station.refinery", in twoRocks);
        Assert.True(matchSplit.Success);
        Assert.Equal("wb:single_mineral", matchSplit.Value.ID);

        //  2 rocks + 1 ice satisfy higher-priority wb:refined_combo without the tag starving the direct requirement.
        var combo = new InventoryComponent(4);
        combo.Contents[0] = new ItemData("wb:ice", 1, 100);
        combo.Contents[1] = new ItemData("wb:rock", 2, 100);
        Result<Recipe> matchCombo = recipes.FindAutoMatch("station.refinery", combo.Contents.AsSpan());
        Assert.True(matchCombo.Success);
        Assert.Equal("wb:refined_combo", matchCombo.Value.ID);

        //  Default/null InventoryComponent contents and large (>128 slot) spans do not throw.
        var emptyDefault = default(InventoryComponent);
        Assert.False(recipes.FindAutoMatch("station.refinery", in emptyDefault).Success);

        var largeSpan = new ItemData[140];
        largeSpan[135] = new ItemData("wb:rock", 2, 100);
        Result<Recipe> largeMatch = recipes.FindAutoMatch("station.refinery", largeSpan.AsSpan());
        Assert.True(largeMatch.Success);
        Assert.Equal("wb:single_mineral", largeMatch.Value.ID);
    }

    [Fact]
    public void InjectorRegistersRecipeDatabaseUnderInterfaces()
    {
        string recipesDir = CreateAssetsDir(
            "di",
            (
                "recipes/base.toml",
                """
                [[recipes]]
                id          = "wb:di_test"
                station_tag = "station.suit"
                mode        = "manual"
                inputs      = [{ item_id = "wb:rock", count = 1 }]
                outputs     = [{ item_id = "wb:panel", count = 1 }]
                """
            )
        );

        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Bricks", "assets")));
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Items", "assets")));
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Skills", "assets")));
        vfs.Mount(new PathInfo(recipesDir));

        using var container = new Container();
        container.RegisterInstance(vfs);
        container.RegisterInstance<ILogger<LocalizedTagsDatabase>>(NullLogger<LocalizedTagsDatabase>.Instance);
        container.RegisterInstance<ILogger<BrickDatabase>>(NullLogger<BrickDatabase>.Instance);
        container.RegisterInstance<ILogger<PaintDatabase>>(NullLogger<PaintDatabase>.Instance);
        container.RegisterInstance<ILogger<ItemDatabase>>(NullLogger<ItemDatabase>.Instance);
        container.RegisterInstance<ILogger<RecipeDatabase>>(NullLogger<RecipeDatabase>.Instance);
        container.RegisterInstance<ILogger<SkillDatabase>>(NullLogger<SkillDatabase>.Instance);
        container.Register<IFileParseService, VirtualFileParseService>(Reuse.Singleton);

        new WaywardBeyond.Bricks.Injector().Inject(container);
        new WaywardBeyond.Items.Injector().Inject(container);
        new WaywardBeyond.Recipes.Injector().Inject(container);
        new WaywardBeyond.Skills.Injector().Inject(container);

        Assert.Empty(container.Validate());

        var byTypedInterface = container.Resolve<IRecipeDatabase>();
        var byAssetInterface = container.Resolve<IAssetDatabase<Recipe>>();

        Assert.Same(byTypedInterface, byAssetInterface);
        Assert.True(byTypedInterface.Get("wb:di_test").Success);
    }

    private string CreateAssetsDir(string name, params (string RelativePath, string Content)[] files)
    {
        string assetsDir = Path.Combine(_tempRoot, name, "assets");
        Directory.CreateDirectory(assetsDir);

        for (var i = 0; i < files.Length; i++)
        {
            string fullPath = Path.Combine(assetsDir, files[i].RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, files[i].Content);
        }

        return assetsDir;
    }

    private static RecipeDatabase LoadRecipes(string recipesAssetsDir, ILogger<RecipeDatabase>? logger = null)
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Items", "assets")));
        vfs.Mount(new PathInfo(recipesAssetsDir));

        IFileParser[] parsers =
        [
            new TomlParser<ItemDefinitions>(),
            new TomlParser<RecipeDefinitions>(),
            new LocalizedTagDefinitionParser(),
        ];
        var parser = new VirtualFileParseService(parsers, vfs);

        var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parser, vfs);
        var items = new ItemDatabase(NullLogger<ItemDatabase>.Instance, parser, vfs);
        return new RecipeDatabase(
            logger ?? NullLogger<RecipeDatabase>.Instance,
            parser,
            vfs,
            items,
            tags
        );
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
