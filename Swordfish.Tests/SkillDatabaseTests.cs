using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Bricks;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Skills;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Headless coverage for the shared skill database: real skill tomls + a virtual file system so the
/// tag-expansion and data-id resolution run against the shipped gameplay data. These are the exact
/// definitions the authoritative server will drive XP from, so they must match the client's worldgen
/// brick ids byte-for-byte (see <see cref="WorldMaterialCatalog"/>).
/// </summary>
public class SkillDatabaseTests
{
    /// <summary>
    /// Builds a headless <see cref="SkillDatabase"/> over the real shared skill tomls. Tests that drive
    /// skill mechanics server-side reuse this so they exercise the shipped definitions.
    /// </summary>
    public static SkillDatabase CreateSharedSkillDatabase()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo("TestFiles/Skills/assets"));

        var parsers = new IFileParser[]
        {
            new TomlParser<SkillDefinitions>(),
            new TomlParser<SkillTagDefinition>(),
        };
        var parseService = new VirtualFileParseService(parsers, vfs);

        return new SkillDatabase(NullLogger<SkillDatabase>.Instance, parseService, vfs);
    }

    [Fact]
    public void LoadsEverySharedSkill()
    {
        SkillDatabase db = CreateSharedSkillDatabase();

        Assert.True(db.Get("mining").Success);
        Assert.True(db.Get("building").Success);
        Assert.True(db.Get("salvaging").Success);
    }

    [Fact]
    public void CarriesLocalizationKeysNotDisplayStrings()
    {
        SkillDatabase db = CreateSharedSkillDatabase();

        SkillData mining = db.Get("mining").Value;

        Assert.Equal("skill.name.mining", mining.Name);
        Assert.Equal("skill.category.gathering", mining.Category);
        Assert.Equal("block/rock.png", mining.Icon);
        Assert.Equal(100, mining.MaxLevel);
    }

    [Fact]
    public void TagSourcesExpandToBrickDataIDs()
    {
        SkillDatabase db = CreateSharedSkillDatabase();
        SkillData mining = db.Get("mining").Value;

        ushort rock = BaseBrickCatalog.Registry.Id("wb:rock");
        ushort ice = BaseBrickCatalog.Registry.Id("wb:ice");

        //  The environment tag drives mining's Break sources.
        Assert.True(mining.TryGetXP(XPSource.Break, rock, out int rockXP));
        Assert.Equal(1, rockXP);
        Assert.True(mining.TryGetXP(XPSource.Break, ice, out int iceXP));
        Assert.Equal(1, iceXP);

        //  No Place source exists for mining.
        Assert.False(mining.TryGetXP(XPSource.Place, rock, out _));
    }

    [Fact]
    public void TagExpansionMatchesWorldgenDataIDs()
    {
        SkillDatabase db = CreateSharedSkillDatabase();
        SkillData mining = db.Get("mining").Value;

        //  Worldgenerated bricks are authored from the same FNV1a rule; a broken rock voxel must yield XP.
        ushort rockDataID = WorldMaterialCatalog.Rock.ID;
        Assert.NotEqual((ushort)0, rockDataID);
        Assert.True(mining.TryGetXP(XPSource.Break, rockDataID, out int xp));
        Assert.Equal(1, xp);
    }

    [Fact]
    public void GroupsSkillsByXPSource()
    {
        SkillDatabase db = CreateSharedSkillDatabase();

        Result<SkillData[]> breaks = db.Get(XPSource.Break);
        Assert.True(breaks.Success);
        Assert.Contains(breaks.Value, skill => skill.ID == "mining");
        Assert.Contains(breaks.Value, skill => skill.ID == "salvaging");

        Result<SkillData[]> places = db.Get(XPSource.Place);
        Assert.True(places.Success);
        Assert.Equal(["building"], places.Value.Select(skill => skill.ID).ToArray());
    }

    [Fact]
    public void CalculateLevelMatchesLegacyCurve()
    {
        SkillDatabase db = CreateSharedSkillDatabase();
        SkillData mining = db.Get("mining").Value;

        //  The mining curve starts 1=61, 2=105, 3=186. Thresholds are strict: level N requires
        //  more total XP than the running sum of the first N increments.
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
        SkillDatabase db = CreateSharedSkillDatabase();

        foreach (string id in new[] { "mining", "building", "salvaging" })
        {
            SkillData skill = db.Get(id).Value;
            int[] levels = skill.Levels.Keys.ToArray();
            Assert.Equal(levels.OrderBy(level => level), levels);
        }
    }
}