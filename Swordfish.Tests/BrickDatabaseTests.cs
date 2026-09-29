using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Bricks;
using WaywardBeyond.Shared.Data;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Headless coverage for the shared brick module: the real brick tomls loaded through a virtual file
/// system, proving <see cref="BrickDatabase"/> owns a deterministic, data-driven id registry over the
/// shipped content, the same ids the client derives.
/// </summary>
public class BrickDatabaseTests
{
    [Fact]
    public void LoadsTheShippedBricks()
    {
        BrickDatabase db = CreateSharedBrickDatabase();

        //  The shipped set is loaded from toml, never from a hardcoded catalog.
        Assert.True(db.Count > 0);
        Assert.NotEqual((ushort)0, db.Id("wb:rock"));
        Assert.NotEqual((ushort)0, db.Id("wb:ice"));
        Assert.NotEqual((ushort)0, db.Id("wb:core"));
    }

    [Fact]
    public void ResolvesNamesAndIdsConsistently()
    {
        BrickDatabase db = CreateSharedBrickDatabase();
        string[] names = ["wb:rock", "wb:ice", "wb:core", "wb:panel"];

        foreach (string name in names)
        {
            ushort id = db.Id(name);
            Assert.NotEqual((ushort)0, id);
            Assert.Equal(name, db.Name(id));
        }
    }

    [Fact]
    public void IsCullerRejectsNonBlockShapes()
    {
        BrickDatabase db = CreateSharedBrickDatabase();

        Voxel rock = new(db.Id("wb:rock"), 0, 0);
        Voxel ice = new(db.Id("wb:ice"), 0, 0);

        Assert.True(db.IsCuller(rock, BrickShape.Block));
        Assert.False(db.IsCuller(ice, BrickShape.Plate));
    }

    public static BrickDatabase CreateSharedBrickDatabase()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo("TestFiles/Bricks/assets"));

        var parsers = new IFileParser[]
        {
            new TomlParser<BrickDefinitions>(),
        };
        var parseService = new VirtualFileParseService(parsers, vfs);

        return new BrickDatabase(NullLogger<BrickDatabase>.Instance, parseService, vfs);
    }
}