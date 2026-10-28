using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using WaywardBeyond.Bodies;
using Xunit;

namespace Swordfish.Tests;

/// <summary>Tests for <see cref="BodyDatabase"/> over the shipped body definitions.</summary>
public class BodyDatabaseTests
{
    private const string BODIES_ROOT = "TestFiles/Bodies/assets";

    [Fact]
    public void LoadsEveryShippedBody()
    {
        BodyDatabase database = Load();

        string[] ids =
        [
            "wb:human_m", "wb:human_f", "wb:bot", "wb:bot2_m", "wb:bot2_f", "wb:bot3",
            "wb:alien_m", "wb:alien_f", "wb:alien2_m", "wb:alien2_f",
            "wb:alien3_m", "wb:alien3_f", "wb:alien4_m", "wb:alien4_f",
        ];

        foreach (string id in ids)
        {
            Assert.True(database.Get(id).Success, id);
        }
    }

    [Fact]
    public void IndexesSpeciesAndGenders()
    {
        BodyDatabase database = Load();

        Assert.Equal(
            ["alien", "alien2", "alien3", "alien4", "bot", "bot2", "bot3", "human"],
            database.Species
        );

        IReadOnlyList<Gender> humanGenders = database.GetGenders("human");
        Assert.Equal([Gender.Male, Gender.Female], humanGenders);

        IReadOnlyList<Gender> botGenders = database.GetGenders("bot");
        Assert.Equal([Gender.Neutral], botGenders);

        IReadOnlyList<Gender> bot3Genders = database.GetGenders("bot3");
        Assert.Equal([Gender.Neutral], bot3Genders);

        Assert.Empty(database.GetGenders("unknown"));
    }

    [Fact]
    public void ExposesSpeciesOnLoadedBodies()
    {
        BodyDatabase database = Load();

        Assert.Equal("human", database.Get("wb:human_m").Value.Species);
        Assert.Equal("human", database.Get("wb:human_f").Value.Species);
        Assert.Equal("bot", database.Get("wb:bot").Value.Species);
    }

    [Fact]
    public void ResolvesBodyBySpeciesAndGender()
    {
        BodyDatabase database = Load();

        Assert.Equal("wb:human_m", database.GetBody("human", Gender.Male)?.ID);
        Assert.Equal("wb:human_f", database.GetBody("human", Gender.Female)?.ID);
        Assert.Equal("wb:bot", database.GetBody("bot", Gender.Neutral)?.ID);
        Assert.Equal("wb:bot3", database.GetBody("bot3", Gender.Neutral)?.ID);

        //  A gender the species lacks falls back to its first variant.
        Assert.Equal("wb:bot", database.GetBody("bot", Gender.Male)?.ID);
        Assert.Equal("wb:bot3", database.GetBody("bot3", Gender.Female)?.ID);

        Assert.Null(database.GetBody("unknown", Gender.Male));
    }

    private static BodyDatabase Load()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(BODIES_ROOT));

        IFileParser[] parsers = [new TomlParser<BodyDefinitions>()];
        var parser = new VirtualFileParseService(parsers, vfs);
        return new BodyDatabase(NullLogger<BodyDatabase>.Instance, parser, vfs);
    }
}
