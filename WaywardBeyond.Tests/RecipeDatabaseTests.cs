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
                [[Recipes]]
                ID              = "wb:smelt_panel"
                TargetTag       = "station.refinery"
                Mode            = "Auto"
                DurationSeconds = 2.5
                PowerKw         = -18.0
                Category        = "recipe.category.hull"
                Priority        = 10

                [[Recipes.Inputs]]
                ItemId    = "wb:rock"
                Count     = 2
                SlotGroup = "wb:input"

                [[Recipes.Outputs]]
                ItemId = "wb:panel"
                Count  = 1
                Chance = 0.75

                [[Recipes]]
                ID              = "wb:burn_ice"
                TargetTag       = "station.generator"
                Mode            = "Auto"
                DurationSeconds = 5
                PowerKw         = 60
                Priority        = 1

                [[Recipes.Inputs]]
                ItemId = "wb:ice"
                Count  = 1

                [[Recipes]]
                ID              = "wb:hand_grate"
                TargetTag       = "station.suit"
                DurationSeconds = 0
                PowerKw         = 0

                [[Recipes.Inputs]]
                ItemId = "tag:item.placeable"
                Count  = 1

                [[Recipes.Outputs]]
                ItemId = "wb:grate"
                Count  = 2
                """
            )
        );

        RecipeDatabase recipes = LoadRecipes(recipesDir);

        Result<Recipe> smeltResult = recipes.Get("wb:smelt_panel");
        Assert.True(smeltResult.Success, smeltResult.Message);
        Recipe smelt = smeltResult.Value;
        Assert.Equal("station.refinery", smelt.TargetTag);
        Assert.Equal(RecipeMode.Auto, smelt.Mode);
        Assert.Equal(2.5f, smelt.DurationSeconds);
        Assert.Equal(-18.0f, smelt.PowerKw);
        Assert.Equal("recipe.category.hull", smelt.Category);
        Assert.Equal(10, smelt.Priority);
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
                [[Recipes]]
                ID              = "wb:valid_first"
                TargetTag       = "station.refinery"
                Mode            = "Manual"
                DurationSeconds = 1.0
                PowerKw         = -5.0
                Inputs          = [{ ItemId = "wb:rock", Count = 1 }]
                Outputs         = [{ ItemId = "wb:panel", Count = 1 }]

                [[Recipes]]
                ID              = "wb:valid_first"
                TargetTag       = "station.refinery"
                Mode            = "Auto"
                DurationSeconds = 2.0
                Inputs          = [{ ItemId = "wb:rock", Count = 2 }]
                Outputs         = [{ ItemId = "wb:grate", Count = 1 }]

                [[Recipes]]
                TargetTag       = "station.refinery"
                Inputs          = [{ ItemId = "wb:rock", Count = 1 }]
                Outputs         = [{ ItemId = "wb:panel", Count = 1 }]

                [[Recipes]]
                ID              = "wb:missing_station"
                Inputs          = [{ ItemId = "wb:rock", Count = 1 }]
                Outputs         = [{ ItemId = "wb:panel", Count = 1 }]

                [[Recipes]]
                ID              = "wb:bad_mode"
                TargetTag       = "station.refinery"
                Mode            = "turbo"
                Inputs          = [{ ItemId = "wb:rock", Count = 1 }]
                Outputs         = [{ ItemId = "wb:panel", Count = 1 }]

                [[Recipes]]
                ID              = "wb:negative_duration"
                TargetTag       = "station.refinery"
                DurationSeconds = -1.0
                Inputs          = [{ ItemId = "wb:rock", Count = 1 }]
                Outputs         = [{ ItemId = "wb:panel", Count = 1 }]

                [[Recipes]]
                ID              = "wb:unknown_input_item"
                TargetTag       = "station.refinery"
                Inputs          = [{ ItemId = "wb:missing_item", Count = 1 }]
                Outputs         = [{ ItemId = "wb:panel", Count = 1 }]

                [[Recipes]]
                ID              = "wb:unknown_input_tag"
                TargetTag       = "station.refinery"
                Inputs          = [{ ItemId = "tag:missing.tag", Count = 1 }]
                Outputs         = [{ ItemId = "wb:panel", Count = 1 }]

                [[Recipes]]
                ID              = "wb:tag_with_unknown_item"
                TargetTag       = "station.refinery"
                Inputs          = [{ ItemId = "tag:recipe.has_missing_item", Count = 1 }]
                Outputs         = [{ ItemId = "wb:panel", Count = 1 }]

                [[Recipes]]
                ID              = "wb:unknown_output_item"
                TargetTag       = "station.refinery"
                Inputs          = [{ ItemId = "wb:rock", Count = 1 }]
                Outputs         = [{ ItemId = "wb:missing_output", Count = 1 }]

                [[Recipes]]
                ID              = "wb:bad_chance"
                TargetTag       = "station.refinery"
                Inputs          = [{ ItemId = "wb:rock", Count = 1 }]
                Outputs         = [{ ItemId = "wb:panel", Count = 1, Chance = 1.5 }]

                [[Recipes]]
                ID              = "wb:no_outputs_or_power"
                TargetTag       = "station.refinery"
                Inputs          = [{ ItemId = "wb:rock", Count = 1 }]
                PowerKw         = -5.0
                """
            ),
            (
                "recipes/corrupt.toml",
                "[[Recipes]\nnot valid toml = = ="
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
    public void GetByTargetTagOrdersByPriorityThenId()
    {
        string recipesDir = CreateAssetsDir(
            "ordering",
            (
                "recipes/ordered.toml",
                """
                [[Recipes]]
                ID        = "wb:z_second"
                TargetTag = "station.factory"
                Priority  = 2
                Inputs    = [{ ItemId = "wb:rock", Count = 1 }]
                Outputs   = [{ ItemId = "wb:panel", Count = 1 }]

                [[Recipes]]
                ID        = "wb:b_tie"
                TargetTag = "station.factory"
                Priority  = 1
                Inputs    = [{ ItemId = "wb:rock", Count = 1 }]
                Outputs   = [{ ItemId = "wb:panel", Count = 1 }]

                [[Recipes]]
                ID        = "wb:a_tie"
                TargetTag = "station.factory"
                Priority  = 1
                Inputs    = [{ ItemId = "wb:rock", Count = 1 }]
                Outputs   = [{ ItemId = "wb:panel", Count = 1 }]

                [[Recipes]]
                ID        = "wb:other_station"
                TargetTag = "station.oven"
                Priority  = 0
                Inputs    = [{ ItemId = "wb:ice", Count = 1 }]
                Outputs   = [{ ItemId = "wb:glass", Count = 1 }]
                """
            )
        );

        RecipeDatabase recipes = LoadRecipes(recipesDir);

        IReadOnlyList<Recipe> factory = recipes.GetByTargetTag("station.factory");
        Assert.Equal(3, factory.Count);
        Assert.Equal("wb:a_tie", factory[0].ID);
        Assert.Equal("wb:b_tie", factory[1].ID);
        Assert.Equal("wb:z_second", factory[2].ID);

        Assert.Single(recipes.GetByTargetTag("station.oven"));
        Assert.Empty(recipes.GetByTargetTag("station.unknown"));
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
                [[Recipes]]
                ID        = "wb:manual_only"
                TargetTag = "station.refinery"
                Mode      = "Manual"
                Priority  = 0
                Inputs    = [{ ItemId = "wb:rock", Count = 1 }]
                Outputs   = [{ ItemId = "wb:panel", Count = 1 }]

                [[Recipes]]
                ID        = "wb:refined_combo"
                TargetTag = "station.refinery"
                Mode      = "Auto"
                Priority  = 1
                Inputs    = [
                    { ItemId = "wb:rock", Count = 2 },
                    { ItemId = "tag:recipe.mineral", Count = 1 },
                ]
                Outputs   = [{ ItemId = "wb:core", Count = 1 }]

                [[Recipes]]
                ID        = "wb:single_mineral"
                TargetTag = "station.refinery"
                Mode      = "Auto"
                Priority  = 2
                Inputs    = [{ ItemId = "tag:recipe.mineral", Count = 2 }]
                Outputs   = [{ ItemId = "wb:glass", Count = 1 }]
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
        var input0 = new SlotId(SlotGroups.INPUT, 0);
        var input1 = new SlotId(SlotGroups.INPUT, 1);
        var input2 = new SlotId(SlotGroups.INPUT, 2);

        InventoryComponent oneRock = InventoryComponent.Create(new SlotGroupDescriptor(SlotGroups.INPUT, 4));
        oneRock.Set(in input0, new ItemData("wb:rock", 1, 100));
        Assert.False(recipes.FindAutoMatch("station.refinery", in oneRock).Success);
        Assert.False(recipes.FindAutoMatch("station.refinery", oneRock.AsSpan()).Success);

        //  2 rocks across split slots satisfy wb:single_mineral (but not wb:refined_combo which needs 3 total).
        InventoryComponent twoRocks = InventoryComponent.Create(new SlotGroupDescriptor(SlotGroups.INPUT, 4));
        twoRocks.Set(in input0, new ItemData("wb:rock", 1, 100));
        twoRocks.Set(in input2, new ItemData("wb:rock", 1, 100));
        Result<Recipe> matchSplit = recipes.FindAutoMatch("station.refinery", in twoRocks);
        Assert.True(matchSplit.Success);
        Assert.Equal("wb:single_mineral", matchSplit.Value.ID);

        //  2 rocks + 1 ice satisfy higher-priority wb:refined_combo without the tag starving the direct requirement.
        InventoryComponent combo = InventoryComponent.Create(new SlotGroupDescriptor(SlotGroups.INPUT, 4));
        combo.Set(in input0, new ItemData("wb:ice", 1, 100));
        combo.Set(in input1, new ItemData("wb:rock", 2, 100));
        Result<Recipe> matchCombo = recipes.FindAutoMatch("station.refinery", combo.AsSpan());
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
                [[Recipes]]
                ID        = "wb:di_test"
                TargetTag = "station.suit"
                Mode      = "Manual"
                Inputs    = [{ ItemId = "wb:rock", Count = 1 }]
                Outputs   = [{ ItemId = "wb:panel", Count = 1 }]
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
