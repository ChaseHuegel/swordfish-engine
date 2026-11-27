using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Items;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Pins the item-to-brick resolution the client and the dedicated server share.</summary>
public class ItemPlaceablesTests
{
    [Fact]
    public void ResolvesTheBrickAnItemPlaces()
    {
        var (items, bricks) = Load();

        Result<Brick> result = ItemPlaceables.ResolvePlaceable(items, bricks, "wb:rock");

        Assert.True(result.Success);
        Assert.Equal("wb:rock", result.Value.ID);
    }

    [Fact]
    public void UnknownItemFails()
    {
        var (items, bricks) = Load();

        Assert.False(ItemPlaceables.ResolvePlaceable(items, bricks, "wb:missing").Success);
    }

    [Fact]
    public void EmptyItemIdFails()
    {
        var (items, bricks) = Load();

        Assert.False(ItemPlaceables.ResolvePlaceable(items, bricks, null).Success);
        Assert.False(ItemPlaceables.ResolvePlaceable(items, bricks, string.Empty).Success);
    }

    [Fact]
    public void ShippedItemsLoadDescriptionKeys()
    {
        var (items, _) = Load();

        Result<Item> rock = items.Get("wb:rock");
        Result<Item> laser = items.Get("laser");
        Result<Item> thruster = items.Get("wb:thruster");

        Assert.True(rock.Success);
        Assert.Equal("item.base.rock.description", rock.Value.Description);
        Assert.True(laser.Success);
        Assert.Equal("item.base.laser.description", laser.Value.Description);
        Assert.True(thruster.Success);
        Assert.Equal("item.base.thruster.description", thruster.Value.Description);
    }

    [Fact]
    public void ShippedItemTagsResolveToKnownItems()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Items", "assets")));

        IFileParser[] parsers =
        [
            new TomlParser<ItemDefinitions>(),
            new LocalizedTagDefinitionParser(),
        ];
        var parser = new VirtualFileParseService(parsers, vfs);
        var items = new ItemDatabase(NullLogger<ItemDatabase>.Instance, parser, vfs);
        var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parser, vfs);
        LocalizedTags invariant = tags.Invariant ?? throw new InvalidOperationException("No invariant tags loaded.");

        Assert.True(invariant.Contains(ItemTags.TOOL, "wb:drill"));
        Assert.True(invariant.Contains(ItemTags.TOOL, "wb:sprayer"));
        Assert.True(invariant.Contains(ItemTags.TOOL_MINING, "laser"));
        Assert.True(invariant.Contains(ItemTags.TOOL_SPRAYER, "wb:sprayer"));
        Assert.True(invariant.Contains(ItemTags.TOOL_DRILL, "wb:drill"));
        Assert.True(invariant.Contains(ItemTags.PLACEABLE, "wb:rock"));
        Assert.False(invariant.Contains(ItemTags.PLACEABLE, "wb:sprayer"));

        foreach (string tag in new[]
        {
            ItemTags.TOOL,
            ItemTags.TOOL_MINING,
            ItemTags.TOOL_SPRAYER,
            ItemTags.TOOL_DRILL,
            ItemTags.PLACEABLE,
        })
        {
            IReadOnlyList<string>? entries = invariant.GetValues(tag);
            Assert.NotNull(entries);
            Assert.NotEmpty(entries!);
            foreach (string itemId in entries!)
            {
                Assert.True(items.Get(itemId).Success, $"Tag \"{tag}\" references unknown item \"{itemId}\".");
            }
        }
    }

    [Fact]
    public void CoreItemResolvesAsBundledBlueprintPlaceableAndNotBrick()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Bricks", "assets")));
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Items", "assets")));

        IFileParser[] parsers =
        [
            new TomlParser<BrickDefinitions>(),
            new TomlParser<ItemDefinitions>(),
            new LocalizedTagDefinitionParser(),
            new BlueprintDataParser(),
        ];
        var parser = new VirtualFileParseService(parsers, vfs);
        var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parser, vfs);
        var bricks = new BrickDatabase(NullLogger<BrickDatabase>.Instance, parser, vfs, tags);
        var items = new ItemDatabase(NullLogger<ItemDatabase>.Instance, parser, vfs);
        var blueprints = new BundledBlueprintDatabase(NullLogger<BundledBlueprintDatabase>.Instance, parser, vfs);

        Result<Item> coreItemResult = items.Get("wb:core");
        Assert.True(coreItemResult.Success);
        Item coreItem = coreItemResult.Value;
        Assert.True(ItemPlaceables.IsBlueprintPlaceable(coreItem));
        Assert.False(ItemPlaceables.IsBrickPlaceable(coreItem));
        Assert.False(ItemPlaceables.ResolvePlaceable(items, bricks, "wb:core").Success);

        Result<string> blueprintId = ItemPlaceables.ResolveBlueprintId(items, "wb:core");
        Assert.True(blueprintId.Success);
        Assert.Equal("builtin/core", blueprintId.Value);

        Result<BlueprintData> resolved = ItemPlaceables.ResolveBlueprint(items, blueprints, "wb:core");
        Assert.True(resolved.Success, resolved.Message);
        Assert.Equal(1, resolved.Value.SizeX);
        Assert.Equal(1, resolved.Value.SizeY);
        Assert.Equal(1, resolved.Value.SizeZ);
        Assert.Equal(1, resolved.Value.BrickCount);
        Assert.NotNull(resolved.Value.BrickPalette);
        Assert.Contains("wb:core", resolved.Value.BrickPalette!);
    }

    [Fact]
    public void BundledBlueprintDatabasePreservesSubdirectoriesNamedBlueprints()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"wb-bp-id-{Guid.NewGuid():N}");
        try
        {
            string assetsDir = Path.Combine(tempRoot, "assets");
            string nestedDir = Path.Combine(assetsDir, "blueprints", "my_blueprints");
            Directory.CreateDirectory(nestedDir);
            string sourceCore = Path.Combine("TestFiles", "Items", "assets", "blueprints", "builtin", "core.wbp");
            File.Copy(sourceCore, Path.Combine(nestedDir, "ship.wbp"));

            var vfs = new VirtualFileSystem();
            vfs.Mount(new PathInfo(assetsDir));
            var parser = new VirtualFileParseService([new BlueprintDataParser()], vfs);
            var blueprints = new BundledBlueprintDatabase(NullLogger<BundledBlueprintDatabase>.Instance, parser, vfs);

            Result<BlueprintData> loaded = blueprints.GetBlueprint("my_blueprints/ship");
            Assert.True(loaded.Success, loaded.Message);
            Assert.False(blueprints.GetBlueprint("ship").Success);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static (IAssetDatabase<Item> Items, IBrickDatabase Bricks) Load()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Bricks", "assets")));
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Items", "assets")));

        IFileParser[] parsers =
        [
            new TomlParser<BrickDefinitions>(),
            new TomlParser<ItemDefinitions>(),
            new LocalizedTagDefinitionParser(),
        ];
        var parser = new VirtualFileParseService(parsers, vfs);

        var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parser, vfs);
        var bricks = new BrickDatabase(NullLogger<BrickDatabase>.Instance, parser, vfs, tags);
        var items = new ItemDatabase(NullLogger<ItemDatabase>.Instance, parser, vfs);
        return (items, bricks);
    }
}
