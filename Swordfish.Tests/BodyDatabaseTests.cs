using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using WaywardBeyond.Bodies;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Headless coverage for the shared body module: the real body tomls loaded through a virtual file
/// system, proving <see cref="BodyDatabase"/> resolves each body's string ID and its tag-driven
/// state/direction texture sets, and that direction order is deterministic (front first).
/// </summary>
public class BodyDatabaseTests
{
    [Fact]
    public void LoadsTheShippedBodies()
    {
        BodyDatabase db = CreateSharedBodyDatabase();

        Assert.True(db.Count > 0);
        Assert.Equal("wb:m_human", db.DefaultId);
    }

    [Fact]
    public void ResolvesStatesAndDirectionsInCanonicalOrder()
    {
        BodyDatabase db = CreateSharedBodyDatabase();

        Result<BodyInfo> result = db.Get("wb:m_human");
        Assert.True(result);
        BodyInfo body = result.Value;

        //  Floating directions honour the canonical order: front (index 0) then back.
        string[] floating = body.GetState("floating");
        Assert.Equal(["characters/m_human_floating.png", "characters/m_human_floating_back.png"], floating);
    }

    [Fact]
    public void ReportsUnknownBodies()
    {
        BodyDatabase db = CreateSharedBodyDatabase();

        Assert.False(db.Contains("wb:does_not_exist"));
        Assert.False(db.Get("wb:does_not_exist"));
    }

    public static BodyDatabase CreateSharedBodyDatabase()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo("TestFiles/Bodies/assets"));

        var parsers = new IFileParser[]
        {
            new TomlParser<BodyModels>(),
        };
        var parseService = new VirtualFileParseService(parsers, vfs);

        return new BodyDatabase(NullLogger<BodyDatabase>.Instance, parseService, vfs);
    }
}