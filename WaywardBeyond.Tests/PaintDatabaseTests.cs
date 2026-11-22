using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Tests for <see cref="PaintDatabase"/> and <see cref="PaintRegistry"/>.</summary>
public class PaintDatabaseTests
{
    private static readonly string BricksAssetsRoot = Path.Combine("TestFiles", "Bricks", "assets");

    [Fact]
    public void LoadsShippedTextureAndColorPaints()
    {
        PaintDatabase database = LoadShipped();

        Assert.Equal(25, database.TexturePaints.Count);
        Assert.Equal(32, database.ColorPaints.Count);

        Result<Paint> panel = database.Get("wb:panel");
        Assert.True(panel.Success);
        Assert.Equal(PaintKind.Texture, panel.Value.Kind);
        Assert.Equal(BrickAlpha.Opaque, panel.Value.Alpha);
        Assert.True(panel.Value.Connected);
        Assert.Equal(["panel.0"], panel.Value.Textures);

        Result<Paint> grate = database.Get("wb:grate");
        Assert.True(grate.Success);
        Assert.Equal(BrickAlpha.Masked, grate.Value.Alpha);

        Result<Paint> glass = database.Get("wb:glass");
        Assert.True(glass.Success);
        Assert.Equal(BrickAlpha.Blend, glass.Value.Alpha);
        Assert.True(glass.Value.Connected);

        Result<Paint> red = database.Get("wb:red");
        Assert.True(red.Success);
        Assert.Equal(PaintKind.Color, red.Value.Kind);
        Assert.True(red.Value.Color.X > 0.8f);
        Assert.True(red.Value.Color.Y < 0.3f);
        Assert.True(red.Value.Color.Z < 0.3f);
    }

    [Fact]
    public void RegistrySortsIdsAndAllocatesStableExtensionIds()
    {
        PaintRegistry registry = PaintRegistry.FromNames(["wb:zebra", "wb:alpha", "wb:mid"]);

        Assert.Equal(3, registry.Count);
        Assert.Equal(1, registry.Id("wb:alpha"));
        Assert.Equal(2, registry.Id("wb:mid"));
        Assert.Equal(3, registry.Id("wb:zebra"));
        Assert.Equal(0, registry.Id("mod:unknown"));

        ushort extA = registry.Resolve("mod:custom_a");
        ushort extB = registry.Resolve("mod:custom_b");
        Assert.Equal(4, extA);
        Assert.Equal(5, extB);
        Assert.Equal(extA, registry.Resolve("mod:custom_a"));
        Assert.Equal("mod:custom_a", registry.Name(extA));
        Assert.Equal("mod:custom_b", registry.Name(extB));
        Assert.Equal(0, registry.Id("mod:custom_a"));
        Assert.Equal(0, registry.Resolve(""));
        Assert.Null(registry.Name(0));
    }

    [Fact]
    public void SkipsInvalidUnionDefinitions()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"wb-paint-tests-{Path.GetRandomFileName()}");
        string paintsDir = Path.Combine(tempDir, "paints");
        Directory.CreateDirectory(paintsDir);

        try
        {
            File.WriteAllText(
                Path.Combine(paintsDir, "mixed.toml"),
                """
                [[Paints]]
                ID = "wb:valid_tex"
                Textures = ["panel.0"]

                [[Paints]]
                ID = "wb:valid_color"
                Color = "#336699"

                [[Paints]]
                ID = "wb:invalid_both"
                Textures = ["panel.0"]
                Color = "#FFFFFF"

                [[Paints]]
                ID = "wb:invalid_neither"

                [[Paints]]
                ID = "wb:invalid_hex"
                Color = "not-a-color"
                """
            );

            var vfs = new VirtualFileSystem();
            vfs.Mount(new PathInfo(tempDir));
            IFileParser[] parsers = [new TomlParser<PaintDefinitions>()];
            var parser = new VirtualFileParseService(parsers, vfs);
            var database = new PaintDatabase(NullLogger<PaintDatabase>.Instance, parser, vfs);

            Assert.True(database.Get("wb:valid_tex").Success);
            Assert.True(database.Get("wb:valid_color").Success);
            Assert.False(database.Get("wb:invalid_both").Success);
            Assert.False(database.Get("wb:invalid_neither").Success);
            Assert.False(database.Get("wb:invalid_hex").Success);
            Assert.Single(database.TexturePaints);
            Assert.Single(database.ColorPaints);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ShippedBricksExposePaintableFlag()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(BricksAssetsRoot));
        IFileParser[] parsers = [new TomlParser<BrickDefinitions>()];
        var parser = new VirtualFileParseService(parsers, vfs);
        var bricks = new BrickDatabase(NullLogger<BrickDatabase>.Instance, parser, vfs);

        Assert.NotEmpty(bricks.Get(_ => true));
        foreach (Brick brick in bricks.Get(_ => true))
        {
            Assert.True(brick.Paintable, $"Brick {brick.ID} must be paintable.");
        }
    }

    private static PaintDatabase LoadShipped()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(BricksAssetsRoot));
        IFileParser[] parsers = [new TomlParser<PaintDefinitions>()];
        var parser = new VirtualFileParseService(parsers, vfs);
        return new PaintDatabase(NullLogger<PaintDatabase>.Instance, parser, vfs);
    }
}
