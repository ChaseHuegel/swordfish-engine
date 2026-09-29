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
/// system, proving <see cref="BrickDatabase"/> owns the deterministic id registry over the shipped
/// content, the same ids the client derives.
/// </summary>
public class BrickDatabaseTests
{
    [Fact]
    public void LoadsEveryShippedBrick()
    {
        BrickDatabase db = CreateSharedBrickDatabase();

        Assert.Equal(BaseBrickCatalog.Names.Count, db.Count);
        foreach (string name in BaseBrickCatalog.Names)
        {
            Assert.True(db.Get(db.Id(name)).Success, $"Brick \"{name}\" should load.");
        }
    }

    [Fact]
    public void ResolvesRegistryIdsConsistentlyWithBaseCatalog()
    {
        BrickDatabase db = CreateSharedBrickDatabase();

        foreach (string name in BaseBrickCatalog.Names)
        {
            Assert.Equal(name, db.Name(db.Id(name)));
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

    private static BrickDatabase CreateSharedBrickDatabase()
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