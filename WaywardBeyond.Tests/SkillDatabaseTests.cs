using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Items;
using WaywardBeyond.Recipes;
using WaywardBeyond.Skills;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>
/// Headless coverage for the shared skill database over the shipped skill and brick tomls: tag
/// expansion against the same registry worldgen writes, per-source indexing, the level curve, and the
/// rejection paths.
/// </summary>
public class SkillDatabaseTests
{
    private const string SHARED_SKILLS_ROOT = "TestFiles/Skills/assets";
    private const string INVALID_SKILLS_ROOT = "TestFiles/SkillsInvalid/assets";

    [Fact]
    public void LoadsEverySharedSkill()
    {
        Fixture fixture = Load(SHARED_SKILLS_ROOT);

        Assert.True(fixture.Skills.Get("mining").Success);
        Assert.True(fixture.Skills.Get("building").Success);
        Assert.True(fixture.Skills.Get("salvaging").Success);
    }

    [Fact]
    public void CarriesLocalizationKeysNotDisplayStrings()
    {
        Skill mining = Load(SHARED_SKILLS_ROOT).Skills.Get("mining").Value;

        Assert.Equal("skill.name.mining", mining.Name);
        Assert.Equal("skill.category.gathering", mining.Category);
        Assert.Equal("skills/mining.png", mining.Icon);
        Assert.Equal(100, mining.Levels.Count);
    }

    [Fact]
    public void ListsAllLoadedSkillsInDeterministicOrderWithSkillArtworkIcons()
    {
        SkillDatabase skills = Load(SHARED_SKILLS_ROOT).Skills;

        Assert.Equal(["building", "mining", "salvaging"], skills.All.Select(skill => skill.ID).ToArray());

        foreach (Skill skill in skills.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(skill.Icon));
            Assert.StartsWith("skills/", skill.Icon);
            string expectedPath = Path.Combine("TestFiles", "Client", "assets", "textures", skill.Icon);
            Assert.True(File.Exists(expectedPath), $"Missing skill icon texture at \"{expectedPath}\".");
        }
    }

    [Fact]
    public void TagSourcesExpandToBrickDataIDs()
    {
        Fixture fixture = Load(SHARED_SKILLS_ROOT);
        Skill mining = fixture.Skills.Get("mining").Value;

        Assert.Equal(1, mining.GetXP(XPSource.Break, fixture.Registry.Id("wb:rock")).Value);
        Assert.Equal(1, mining.GetXP(XPSource.Break, fixture.Registry.Id("wb:ice")).Value);

        //  Mining has no place source.
        Assert.False(mining.GetXP(XPSource.Place, fixture.Registry.Id("wb:rock")).Success);
    }

    [Fact]
    public void TagExpansionMatchesWorldgenDataIDs()
    {
        Fixture fixture = Load(SHARED_SKILLS_ROOT);
        Skill mining = fixture.Skills.Get("mining").Value;

        //  Worldgen writes brick ids through the same registry, so a generated rock voxel yields XP.
        ushort rock = fixture.Registry.Id("wb:rock");
        Assert.NotEqual((ushort)0, rock);
        Assert.True(mining.GetXP(XPSource.Break, rock).Success);
    }

    [Fact]
    public void GroupsSkillsByXPSource()
    {
        SkillDatabase skills = Load(SHARED_SKILLS_ROOT).Skills;

        Assert.Equal(["building"], skills.Get(XPSource.Place).Select(skill => skill.ID).ToArray());
        Assert.Equal(
            ["mining", "salvaging"],
            skills.Get(XPSource.Break).Select(skill => skill.ID).OrderBy(id => id).ToArray());
    }

    [Fact]
    public void CalculateLevelMatchesLegacyCurve()
    {
        Skill mining = Load(SHARED_SKILLS_ROOT).Skills.Get("mining").Value;

        //  Thresholds are strict: a level needs more total XP than the running sum of its increments.
        Assert.Equal(new LevelInfo(0, 0), mining.CalculateLevel(0));
        Assert.Equal(new LevelInfo(0, 61), mining.CalculateLevel(61));
        Assert.Equal(new LevelInfo(1, 1), mining.CalculateLevel(62));
        Assert.Equal(new LevelInfo(1, 44), mining.CalculateLevel(105));
        Assert.Equal(new LevelInfo(1, 105), mining.CalculateLevel(166));
        Assert.Equal(new LevelInfo(2, 1), mining.CalculateLevel(167));
    }

    [Fact]
    public void LevelCurveIsSortedByLevel()
    {
        SkillDatabase skills = Load(SHARED_SKILLS_ROOT).Skills;

        foreach (string id in new[] { "mining", "building", "salvaging" })
        {
            int[] levels = [.. skills.Get(id).Value.Levels.Keys];
            Assert.Equal([.. levels.OrderBy(level => level)], levels);
        }
    }

    [Fact]
    public void RejectsInvalidSkillDefinitions()
    {
        SkillDatabase skills = Load(INVALID_SKILLS_ROOT).Skills;

        //  A valid row in the same file still loads; each invalid row is rejected on its own.
        Assert.True(skills.Get("valid").Success);
        Assert.False(skills.Get("no_levels").Success);
        Assert.False(skills.Get("non_positive").Success);
    }

    [Fact]
    public void DuplicateSkillIDIsIndexedOnce()
    {
        Fixture fixture = Load(INVALID_SKILLS_ROOT);

        Skill[] duplicates = [.. fixture.Skills.Get(XPSource.Break).Where(skill => skill.ID == "duplicate")];

        //  The second declaration is rejected, so one brick cannot grant its XP twice.
        Assert.Single(duplicates);
        Assert.Equal(5, duplicates[0].GetXP(XPSource.Break, fixture.Registry.Id("wb:rock")).Value);
    }

    [Fact]
    public void EvaluatesCraftingAndProcessingXpByStationRecipeAndOutputTagWithoutCouplingRecipes()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"wb-skill-craft-{Guid.NewGuid():N}");
        try
        {
            string assetsDir = Path.Combine(tempRoot, "assets");
            Directory.CreateDirectory(Path.Combine(assetsDir, "skills"));
            Directory.CreateDirectory(Path.Combine(assetsDir, "recipes"));

            File.WriteAllText(
                Path.Combine(assetsDir, "recipes", "craft.toml"),
                """
                [[recipes]]
                id          = "wb:craft_core"
                station_tag = "station.synthesizer"
                mode        = "manual"
                inputs      = [{ item_id = "wb:rock", count = 2 }]
                outputs     = [{ item_id = "wb:panel", count = 1 }]

                [[recipes]]
                id          = "wb:craft_grate"
                station_tag = "station.synthesizer"
                mode        = "manual"
                inputs      = [{ item_id = "wb:rock", count = 1 }]
                outputs     = [{ item_id = "wb:grate", count = 1 }]

                [[recipes]]
                id          = "wb:smelt_laser"
                station_tag = "station.refinery"
                mode        = "auto"
                inputs      = [{ item_id = "wb:rock", count = 1 }]
                outputs     = [{ item_id = "laser", count = 1 }]

                [[recipes]]
                id          = "wb:burn_ice"
                station_tag = "station.generator"
                mode        = "auto"
                power_kw    = 45.0
                inputs      = [{ item_id = "wb:ice", count = 1 }]
                """
            );

            File.WriteAllText(
                Path.Combine(assetsDir, "skills", "crafting.toml"),
                """
                [[Skills]]
                ID       = "manufacturing"
                Name     = "skill.name.manufacturing"
                Category = "skill.category.crafting"

                [Skills.Sources.Craft]
                "station:station.synthesizer" = 5
                "tag:item.placeable"          = 8
                "wb:panel"                    = 12
                "recipe:wb:craft_core"        = 25

                [Skills.Sources.Process]
                "station:station.refinery"  = 4
                "station:station.generator" = 2
                "tag:item.tool.mining"      = 15

                [Skills.Levels]
                1 = 50
                """
            );

            var vfs = new VirtualFileSystem();
            vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Bricks", "assets")));
            vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Items", "assets")));
            vfs.Mount(new PathInfo(assetsDir));

            IFileParser[] parsers =
            [
                new TomlParser<BrickDefinitions>(),
                new TomlParser<ItemDefinitions>(),
                new TomlParser<RecipeDefinitions>(),
                new TomlParser<SkillDefinitions>(),
                new LocalizedTagDefinitionParser(),
            ];
            var parser = new VirtualFileParseService(parsers, vfs);

            var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parser, vfs);
            var bricks = new BrickDatabase(NullLogger<BrickDatabase>.Instance, parser, vfs, tags);
            var items = new ItemDatabase(NullLogger<ItemDatabase>.Instance, parser, vfs);
            var recipes = new RecipeDatabase(NullLogger<RecipeDatabase>.Instance, parser, vfs, items, tags);
            var skills = new SkillDatabase(NullLogger<SkillDatabase>.Instance, parser, vfs, bricks, tags);

            Skill manufacturing = skills.Get("manufacturing").Value;
            Assert.Contains(skills.Get(XPSource.Craft), static s => s.ID == "manufacturing");
            Assert.Contains(skills.Get(XPSource.Process), static s => s.ID == "manufacturing");

            //  Precedence: recipe:<id> (25) > direct output item (12) > output item tag (8) > station:<tag> (5).
            Recipe coreRecipe = recipes.Get("wb:craft_core").Value;
            Assert.Equal(25, manufacturing.GetXP(XPSource.Craft, coreRecipe.ID, coreRecipe.StationTag, coreRecipe.Outputs[0].ItemId).Value);

            //  Direct output item override applies when recipe is not overridden.
            Assert.Equal(12, manufacturing.GetXP(XPSource.Craft, "wb:other_panel_recipe", "station.synthesizer", "wb:panel").Value);

            //  Output item tag applies over station fallback when no recipe or direct output entry matches.
            Recipe grateRecipe = recipes.Get("wb:craft_grate").Value;
            Assert.Equal(8, manufacturing.GetXP(XPSource.Craft, grateRecipe.ID, grateRecipe.StationTag, grateRecipe.Outputs[0].ItemId).Value);

            //  Station fallback applies when output item matches neither direct nor tag rules.
            Assert.Equal(5, manufacturing.GetXP(XPSource.Craft, "wb:untagged_craft", "station.synthesizer", "laser").Value);

            //  Process source handles output item tag and 0-output generator station fallback.
            Recipe laserSmelt = recipes.Get("wb:smelt_laser").Value;
            Assert.Equal(15, manufacturing.GetXP(XPSource.Process, laserSmelt.ID, laserSmelt.StationTag, [laserSmelt.Outputs[0].ItemId]).Value);

            Recipe genRecipe = recipes.Get("wb:burn_ice").Value;
            Assert.Equal(2, manufacturing.GetXP(XPSource.Process, genRecipe.ID, genRecipe.StationTag).Value);

            //  Direct output match applies regardless of station; unmatched station and unknown output yield no XP.
            Assert.Equal(12, manufacturing.GetXP(XPSource.Craft, "wb:none", "station.oven", "wb:panel").Value);
            Assert.False(manufacturing.GetXP(XPSource.Craft, "wb:none", "station.oven", "wb:unknown").Success);
            Assert.False(manufacturing.GetXP(XPSource.Craft, "wb:burn_ice", "station.generator").Success);
            Assert.False(manufacturing.GetXP(XPSource.Break, bricks.Id("wb:rock")).Success);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void EvaluatesDirectRecipeKeyAndMultiOutputSpanInCraftSources()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"wb-skill-direct-{Guid.NewGuid():N}");
        try
        {
            string assetsDir = Path.Combine(tempRoot, "assets");
            Directory.CreateDirectory(Path.Combine(assetsDir, "skills"));

            File.WriteAllText(
                Path.Combine(assetsDir, "skills", "cooking.toml"),
                """
                [[Skills]]
                ID       = "cooking"
                Name     = "skill.name.cooking"
                Category = "skill.category.crafting"

                [Skills.Sources.Craft]
                "wb:direct_recipe_id" = 14
                "wb:byproduct_item"   = 6
                "tag:missing.tag"     = 99

                [Skills.Levels]
                1 = 100
                """
            );

            var vfs = new VirtualFileSystem();
            vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Bricks", "assets")));
            vfs.Mount(new PathInfo(assetsDir));

            IFileParser[] parsers =
            [
                new TomlParser<BrickDefinitions>(),
                new TomlParser<SkillDefinitions>(),
                new LocalizedTagDefinitionParser(),
            ];
            var parser = new VirtualFileParseService(parsers, vfs);

            var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parser, vfs);
            var bricks = new BrickDatabase(NullLogger<BrickDatabase>.Instance, parser, vfs, tags);
            var skills = new SkillDatabase(NullLogger<SkillDatabase>.Instance, parser, vfs, bricks, tags);

            Skill cooking = skills.Get("cooking").Value;

            Result<int> byRecipe = cooking.GetXP(XPSource.Craft, "wb:direct_recipe_id", "station.oven", "wb:other");
            Assert.True(byRecipe.Success);
            Assert.Equal(14, byRecipe.Value);

            Result<int> byMultiOutput = cooking.GetXP(XPSource.Craft, "wb:other_recipe", "station.oven", ["wb:plain", "wb:byproduct_item"]);
            Assert.True(byMultiOutput.Success);
            Assert.Equal(6, byMultiOutput.Value);

            Assert.False(cooking.GetXP(XPSource.Craft, "wb:other_recipe", "station.oven", "wb:plain").Success);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>Loads the shipped skill definitions and the registry they resolve against.</summary>
    internal static (SkillDatabase Skills, IBrickRegistry Registry) CreateSharedFixture()
    {
        Fixture fixture = Load(SHARED_SKILLS_ROOT);
        return (fixture.Skills, fixture.Registry);
    }

    private static Fixture Load(string skillsRoot)
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Bricks", "assets")));
        vfs.Mount(new PathInfo(skillsRoot));

        IFileParser[] parsers =
        [
            new TomlParser<SkillDefinitions>(),
            new TomlParser<BrickDefinitions>(),
            new LocalizedTagDefinitionParser(),
        ];
        var parser = new VirtualFileParseService(parsers, vfs);

        var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parser, vfs);
        var bricks = new BrickDatabase(NullLogger<BrickDatabase>.Instance, parser, vfs, tags);
        var skills = new SkillDatabase(NullLogger<SkillDatabase>.Instance, parser, vfs, bricks, tags);
        return new Fixture(skills, bricks);
    }

    private readonly record struct Fixture(SkillDatabase Skills, IBrickRegistry Registry);
}
