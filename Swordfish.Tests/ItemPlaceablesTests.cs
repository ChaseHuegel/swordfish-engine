using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Items;
using Xunit;

namespace Swordfish.Tests;

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

    private static (IAssetDatabase<Item> Items, IBrickDatabase Bricks) Load()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Bricks", "assets")));
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Items", "assets")));

        IFileParser[] parsers =
        [
            new TomlParser<BrickDefinitions>(),
            new TomlParser<ItemDefinitions>(),
        ];
        var parser = new VirtualFileParseService(parsers, vfs);

        var bricks = new BrickDatabase(NullLogger<BrickDatabase>.Instance, parser, vfs);
        var items = new ItemDatabase(NullLogger<ItemDatabase>.Instance, parser, vfs);
        return (items, bricks);
    }
}
