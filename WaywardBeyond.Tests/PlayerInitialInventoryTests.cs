#nullable enable
using WaywardBeyond.Data;
using WaywardBeyond.Server.Systems;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>
/// The server grants a mode-appropriate starter inventory to a character with no saved inventory and
/// never restocks a saved one in either mode.
/// </summary>
public class PlayerInitialInventoryTests
{
    [Fact]
    public void AdventureStarterInventoryIsTheMiningLaserOnly()
    {
        ItemData[] starter = PlayerInitialInventory.StarterInventory(GameMode.Adventure);

        ItemData laser = Assert.Single(starter);
        Assert.Equal("laser", laser.ID);
        Assert.Equal(1, laser.Count);
        Assert.Equal(1, laser.MaxSize);
    }

    [Fact]
    public void AdventureResolveGrantsTheMiningLaserOnly()
    {
        ItemData[] resolved = PlayerInitialInventory.Resolve(seedContents: null, GameMode.Adventure);

        ItemData laser = Assert.Single(resolved);
        Assert.Equal("laser", laser.ID);
        Assert.Equal(1, laser.Count);
    }

    [Fact]
    public void CreativeResolveGrantsTheFullCreativeLoadout()
    {
        ItemData[] resolved = PlayerInitialInventory.Resolve(seedContents: null, GameMode.Creative);

        Assert.Equal(PlayerInitialInventory.StarterInventory(GameMode.Creative), resolved);
        Assert.Contains(resolved, item => item.ID == "laser");
        Assert.Contains(resolved, item => item.ID == "wb:sprayer");
        Assert.Contains(resolved, item => item.ID == "wb:drill");
        Assert.Contains(resolved, item => item.ID == "wb:projector");
        Assert.Contains(resolved, item => item.ID == "wb:rock" && item.Count == 100);
        Assert.True(resolved.Length > 1);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(-1)]
    public void UndefinedGameModeFallsBackToAdventureStarterInventory(int undefinedMode)
    {
        ItemData[] resolved = PlayerInitialInventory.Resolve(seedContents: null, (GameMode)undefinedMode);

        ItemData laser = Assert.Single(resolved);
        Assert.Equal("laser", laser.ID);
    }

    [Fact]
    public void SavedInventoryIsPreservedInAdventure()
    {
        ItemData[] saved = [new ItemData("wb:rock", 5, 100), new ItemData("wb:panel", 2, 100)];

        ItemData[] resolved = PlayerInitialInventory.Resolve(saved, GameMode.Adventure);

        Assert.Equal(saved, resolved);
    }

    [Fact]
    public void EmptySavedInventoryIsPreservedInCreative()
    {
        ItemData[] saved = [];

        ItemData[] resolved = PlayerInitialInventory.Resolve(saved, GameMode.Creative);

        Assert.Empty(resolved);
    }
}
