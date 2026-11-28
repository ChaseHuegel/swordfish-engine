#nullable enable
using System.IO;
using System.Linq;
using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Items;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Voxels;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Tests the paint sprayer item definition, starter inventory inclusion, and face-normal mapping.</summary>
public class SprayerToolTests
{
    [Fact]
    public void ShippedSprayerItemLoadsWithSprayerToolTypeAndIsInStarterInventory()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Items", "assets")));
        IFileParser[] parsers = [new TomlParser<ItemDefinitions>()];
        var parser = new VirtualFileParseService(parsers, vfs);
        var items = new ItemDatabase(NullLogger<ItemDatabase>.Instance, parser, vfs);

        Result<Item> sprayer = items.Get("wb:sprayer");
        Assert.True(sprayer.Success, "Shipped wb:sprayer item must load.");
        Assert.NotNull(sprayer.Value.Tool);
        Assert.Equal(ToolType.Sprayer, sprayer.Value.Tool!.Value.Type);

        Result<Item> drill = items.Get("wb:drill");
        Assert.True(drill.Success, "Shipped wb:drill item must load.");
        Assert.NotNull(drill.Value.Tool);
        Assert.Equal(ToolType.Drill, drill.Value.Tool!.Value.Type);

        Assert.Contains(PlayerInitialInventory.StarterInventory(GameMode.Creative), entry => entry.Item.ID == "wb:sprayer");
        Assert.Contains(PlayerInitialInventory.StarterInventory(GameMode.Creative), entry => entry.Item.ID == "wb:drill");
    }

    [Fact]
    public void WorldNormalToBrickFaceAccountsForStructureAndVoxelOrientation()
    {
        //  Identity structure + identity voxel: +Z world normal -> Front.
        Assert.Equal(
            BrickFace.Front,
            PaintInteractionResolver.WorldNormalToBrickFace(Vector3.UnitZ, Quaternion.Identity, Orientation.Identity)
        );

        //  Voxel yawed 90 degrees (+Z rotates to -X in world): hitting -X in world is the voxel's Front face.
        var yaw90 = new Orientation(pitch: 0, yaw: 1, roll: 0);
        Vector3 rotatedFrontWorld = Vector3.Transform(Vector3.UnitZ, yaw90.ToQuaternion());
        Assert.Equal(
            BrickFace.Front,
            PaintInteractionResolver.WorldNormalToBrickFace(rotatedFrontWorld, Quaternion.Identity, yaw90)
        );
    }
}
