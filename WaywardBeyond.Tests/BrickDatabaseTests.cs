using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Voxels;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Client.Voxels.Models;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Voxels;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Tests for <see cref="BrickDatabase"/> and <see cref="BrickAlpha"/> over shipped brick definitions.</summary>
public class BrickDatabaseTests
{
    private static (BrickDatabase Bricks, LocalizedTagsDatabase Tags) LoadWithTags()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Bricks", "assets")));

        IFileParser[] parsers = [new TomlParser<BrickDefinitions>(), new LocalizedTagDefinitionParser()];
        var parser = new VirtualFileParseService(parsers, vfs);
        var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parser, vfs);
        var bricks = new BrickDatabase(NullLogger<BrickDatabase>.Instance, parser, vfs, tags);
        return (bricks, tags);
    }

    private static BrickDatabase Load() => LoadWithTags().Bricks;

    [Fact]
    public void LoadsShippedBrickAlphaModes()
    {
        BrickDatabase database = Load();

        Assert.Equal(BrickAlpha.Blend, database.Get(database.Id("wb:glass")).Value.Alpha);
        Assert.Equal(BrickAlpha.Opaque, database.Get(database.Id("wb:light")).Value.Alpha);
        Assert.False(database.Get(database.Id("wb:light")).Value.CastShadows);
        Assert.Equal(BrickAlpha.Opaque, database.Get(database.Id("wb:small_light")).Value.Alpha);
        Assert.False(database.Get(database.Id("wb:small_light")).Value.CastShadows);
        Assert.Equal(BrickAlpha.Masked, database.Get(database.Id("wb:grate")).Value.Alpha);
        Assert.Equal(BrickAlpha.Masked, database.Get(database.Id("wb:truss")).Value.Alpha);
        Assert.Equal(BrickAlpha.Masked, database.Get(database.Id("wb:porthole")).Value.Alpha);
        Assert.Equal(BrickAlpha.Opaque, database.Get(database.Id("wb:rock")).Value.Alpha);
        Assert.True(database.Get(database.Id("wb:rock")).Value.CastShadows);
        Assert.Equal(BrickAlpha.Opaque, database.Get(database.Id("wb:panel")).Value.Alpha);
        Assert.Equal(BrickAlpha.Opaque, database.Get(database.Id("wb:vent")).Value.Alpha);
    }

    [Fact]
    public void LoadsShippedBrickSounds()
    {
        BrickDatabase database = Load();

        Brick panel = database.Get(database.Id("wb:panel")).Value;
        Assert.Equal(new[] { "metal_place" }, panel.Sounds[BrickEventKind.Place]);
        Assert.Equal(new[] { "metal_break" }, panel.Sounds[BrickEventKind.Break]);

        Brick rock = database.Get(database.Id("wb:rock")).Value;
        Assert.Equal(new[] { "rock_place" }, rock.Sounds[BrickEventKind.Place]);
        Assert.Equal(new[] { "rock_break" }, rock.Sounds[BrickEventKind.Break]);

        Brick ice = database.Get(database.Id("wb:ice")).Value;
        Assert.Equal(new[] { "rock_place" }, ice.Sounds[BrickEventKind.Place]);
        Assert.Equal(new[] { "rock_break" }, ice.Sounds[BrickEventKind.Break]);
    }

    [Fact]
    public void LoadsBrickColorAndFallsBackToWhite()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"wb-brick-tests-{Path.GetRandomFileName()}");
        string bricksDir = Path.Combine(tempDir, "bricks");
        Directory.CreateDirectory(bricksDir);

        try
        {
            File.WriteAllText(
                Path.Combine(bricksDir, "colors.toml"),
                """
                [[Bricks]]
                ID = "wb:amber"
                Color = "#FF8000"

                [[Bricks]]
                ID = "wb:plain"

                [[Bricks]]
                ID = "wb:invalid"
                Color = "not-a-color"
                """
            );

            var vfs = new VirtualFileSystem();
            vfs.Mount(new PathInfo(tempDir));
            IFileParser[] parsers = [new TomlParser<BrickDefinitions>(), new LocalizedTagDefinitionParser()];
            var parser = new VirtualFileParseService(parsers, vfs);
            var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parser, vfs);
            var bricks = new BrickDatabase(NullLogger<BrickDatabase>.Instance, parser, vfs, tags);

            Brick amber = bricks.Get(bricks.Id("wb:amber")).Value;
            Assert.Equal(1f, amber.Color.X);
            Assert.Equal(128f / 255f, amber.Color.Y);
            Assert.Equal(0f, amber.Color.Z);
            Assert.Equal(1f, amber.LinearColor.X, precision: 3);
            Assert.Equal(0.216f, amber.LinearColor.Y, precision: 3);
            Assert.Equal(0f, amber.LinearColor.Z);

            Assert.Equal(Vector3.One, bricks.Get(bricks.Id("wb:plain")).Value.Color);
            Assert.Equal(Vector3.One, bricks.Get(bricks.Id("wb:invalid")).Value.Color);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ShippedBricksDefaultToWhite()
    {
        BrickDatabase database = Load();

        foreach (Brick brick in database.Get(_ => true))
        {
            Assert.Equal(Vector3.One, brick.Color);
            Assert.Equal(Vector3.One, brick.LinearColor);
        }
    }

    [Fact]
    public void IsCullerOnlyAllowsOpaqueBlocks()
    {
        BrickDatabase database = Load();

        ushort rockId = database.Id("wb:rock");
        ushort grateId = database.Id("wb:grate");
        ushort glassId = database.Id("wb:glass");
        ushort lightId = database.Id("wb:light");
        ushort smallLightId = database.Id("wb:small_light");

        var rockVoxel = new Voxel { ID = rockId };
        var grateVoxel = new Voxel { ID = grateId };
        var glassVoxel = new Voxel { ID = glassId };
        var lightVoxel = new Voxel { ID = lightId };
        var smallLightVoxel = new Voxel { ID = smallLightId };

        Assert.True(database.IsCuller(rockVoxel, BrickShape.Block));
        Assert.False(database.IsCuller(rockVoxel, BrickShape.Slab));
        Assert.False(database.IsCuller(grateVoxel, BrickShape.Block));
        Assert.False(database.IsCuller(glassVoxel, BrickShape.Block));
        Assert.False(database.IsCuller(lightVoxel, BrickShape.Block));
        Assert.False(database.IsCuller(smallLightVoxel, BrickShape.Block));
    }

    [Fact]
    public void ShippedBricksResolveInvariantTagProperties()
    {
        (BrickDatabase database, LocalizedTagsDatabase tags) = LoadWithTags();
        LocalizedTags invariant = tags.Invariant ?? throw new InvalidOperationException("No invariant tags loaded.");

        //  Light voxel flood-fill comes from brick.light; point-light entity config lives on [Bricks.Entity.Light].
        Brick lightBrick = database.Get("wb:light").Value;
        Brick smallLightBrick = database.Get("wb:small_light").Value;
        Brick panelBrick = database.Get("wb:panel").Value;
        Assert.True(lightBrick.LightSource);
        Assert.Equal(15, lightBrick.Brightness);
        Assert.NotNull(lightBrick.Entity?.Light);
        Assert.True(lightBrick.Entity!.Light!.Value.TintFromPaint);
        Assert.True(smallLightBrick.LightSource);
        Assert.NotNull(smallLightBrick.Entity?.Light);
        Assert.False(panelBrick.LightSource);
        Assert.Null(panelBrick.Entity);

        //  Thruster declares [Bricks.Entity.Light], is orientable, and is listed under brick.thruster.
        Brick thruster = database.Get("wb:thruster").Value;
        Assert.NotNull(thruster.Entity?.Light);
        Assert.Equal(0.75f, thruster.Entity!.Light!.Value.Radius);
        Assert.Equal(20f, thruster.Entity.Light.Value.Intensity);
        Assert.False(thruster.LightSource);
        Assert.True(thruster.IsOrientable(BrickShape.Custom));
        Assert.True(invariant.Contains(BrickTags.THRUSTER, "wb:thruster"));
        Assert.False(invariant.Contains(BrickTags.THRUSTER, "wb:panel"));

        //  Orientable blocks carry brick.orientable; non-orientable blocks do not.
        Assert.True(database.Get("wb:storage").Value.IsOrientable(BrickShape.Block));
        Assert.True(database.Get("wb:control_buttons").Value.IsOrientable(BrickShape.Block));
        Assert.False(database.Get("wb:core").Value.IsOrientable(BrickShape.Block));
        Assert.False(database.Get("wb:panel").Value.IsOrientable(BrickShape.Block));
        Assert.True(database.Get("wb:panel").Value.IsOrientable(BrickShape.Slope));

        //  Environment, buildable, and manufactured groups match shipped bricks.
        Assert.Equal(["wb:ice", "wb:rock"], invariant.GetValues(BrickTags.ENVIRONMENT)!.OrderBy(id => id).ToArray());
        Assert.Equal(database.Get(_ => true).Count, invariant.GetValues(BrickTags.BUILDABLE)!.Count);
        Assert.Equal(database.Get(_ => true).Count - 2, invariant.GetValues(BrickTags.MANUFACTURED)!.Count);

        //  Every entry in every brick.* tag resolves to a real shipped brick.
        foreach (string tag in new[]
        {
            BrickTags.LIGHT,
            BrickTags.AIRTIGHT,
            BrickTags.TOGGLEABLE,
            BrickTags.ORIENTABLE,
            BrickTags.PAINTABLE,
            BrickTags.THRUSTER,
            BrickTags.BUILDABLE,
            BrickTags.ENVIRONMENT,
            BrickTags.MANUFACTURED,
        })
        {
            var entries = invariant.GetValues(tag);
            Assert.NotNull(entries);
            foreach (string brickId in entries!)
            {
                Assert.True(database.Get(brickId).Success, $"Tag \"{tag}\" references unknown brick \"{brickId}\".");
            }
        }
    }

    [Fact]
    public void LightEntityDecoratorAppliesConfiguredLightToThrusterAndSkipsPanel()
    {
        (BrickDatabase database, _) = LoadWithTags();
        var decorator = new LightEntityDecorator(database);

        var store = new DataStore();
        int parent = store.Alloc();
        int thrusterEntity = store.Alloc();
        int panelEntity = store.Alloc();
        var voxelComponent = new VoxelComponent(new VoxelObject(16), new VoxelChunkRegistry());

        Brick thrusterBrick = database.Get("wb:thruster").Value;
        Brick panelBrick = database.Get("wb:panel").Value;
        var thrusterVoxel = new Voxel { ID = thrusterBrick.DataID };
        var panelVoxel = new Voxel { ID = panelBrick.DataID };

        Assert.True(decorator.AppliesTo(thrusterBrick));
        Assert.False(decorator.AppliesTo(panelBrick));

        decorator.Process(store, parent, thrusterEntity, voxelComponent, new VoxelInfo(0, 0, 0, thrusterVoxel), thrusterBrick);
        decorator.Process(store, parent, panelEntity, voxelComponent, new VoxelInfo(1, 0, 0, panelVoxel), panelBrick);

        Assert.True(store.TryGet(thrusterEntity, out LightComponent thrusterLight));
        Assert.Equal(0.75f, thrusterLight.Radius);
        Assert.Equal(0.25f, thrusterLight.Size);
        Assert.Equal(20f, thrusterLight.Intensity);
        Assert.False(thrusterLight.CastShadows);
        Assert.False(store.TryGet(panelEntity, out LightComponent _));
    }
}
