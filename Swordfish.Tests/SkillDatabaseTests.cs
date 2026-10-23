using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Skills;
using Xunit;

namespace Swordfish.Tests;

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
        Assert.Equal("block/rock.png", mining.Icon);
        Assert.Equal(100, mining.Levels.Count);
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

        var bricks = new BrickDatabase(NullLogger<BrickDatabase>.Instance, parser, vfs);
        var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parser, vfs);
        var skills = new SkillDatabase(NullLogger<SkillDatabase>.Instance, parser, vfs, bricks, tags);
        return new Fixture(skills, bricks);
    }

    private readonly record struct Fixture(SkillDatabase Skills, IBrickRegistry Registry);
}
