using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using Xunit;

namespace Swordfish.Tests;

/// <summary>Tests for <see cref="BrickDatabase"/> and <see cref="BrickAlpha"/> over shipped brick definitions.</summary>
public class BrickDatabaseTests
{
    private static BrickDatabase Load()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Bricks", "assets")));

        IFileParser[] parsers = [new TomlParser<BrickDefinitions>()];
        var parser = new VirtualFileParseService(parsers, vfs);
        return new BrickDatabase(NullLogger<BrickDatabase>.Instance, parser, vfs);
    }

    [Fact]
    public void LoadsShippedBrickAlphaModes()
    {
        BrickDatabase database = Load();

        Assert.Equal(BrickAlpha.Blend, database.Get(database.Id("wb:glass")).Value.Alpha);
        Assert.Equal(BrickAlpha.Masked, database.Get(database.Id("wb:grate")).Value.Alpha);
        Assert.Equal(BrickAlpha.Masked, database.Get(database.Id("wb:truss")).Value.Alpha);
        Assert.Equal(BrickAlpha.Masked, database.Get(database.Id("wb:porthole")).Value.Alpha);
        Assert.Equal(BrickAlpha.Opaque, database.Get(database.Id("wb:rock")).Value.Alpha);
        Assert.Equal(BrickAlpha.Opaque, database.Get(database.Id("wb:panel")).Value.Alpha);
        Assert.Equal(BrickAlpha.Opaque, database.Get(database.Id("wb:vent")).Value.Alpha);
    }

    [Fact]
    public void IsCullerOnlyAllowsOpaqueBlocks()
    {
        BrickDatabase database = Load();

        ushort rockId = database.Id("wb:rock");
        ushort grateId = database.Id("wb:grate");
        ushort glassId = database.Id("wb:glass");

        var rockVoxel = new Voxel { ID = rockId };
        var grateVoxel = new Voxel { ID = grateId };
        var glassVoxel = new Voxel { ID = glassId };

        Assert.True(database.IsCuller(rockVoxel, BrickShape.Block));
        Assert.False(database.IsCuller(rockVoxel, BrickShape.Slab));
        Assert.False(database.IsCuller(grateVoxel, BrickShape.Block));
        Assert.False(database.IsCuller(glassVoxel, BrickShape.Block));
    }
}
