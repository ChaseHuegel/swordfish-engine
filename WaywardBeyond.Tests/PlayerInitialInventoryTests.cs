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
        KeyedSlotEntry[] starter = PlayerInitialInventory.StarterInventory(GameMode.Adventure);

        KeyedSlotEntry entry = Assert.Single(starter);
        Assert.Equal(SlotGroups.HOTBAR, entry.GroupId);
        Assert.Equal(0, entry.LocalIndex);
        Assert.Equal("laser", entry.Item.ID);
        Assert.Equal(1, entry.Item.Count);
        Assert.Equal(1, entry.Item.MaxSize);
    }

    [Fact]
    public void AdventureResolveGrantsTheMiningLaserOnly()
    {
        KeyedSlotEntry[] resolved = PlayerInitialInventory.Resolve(keyedSeed: null, legacySeed: null, GameMode.Adventure);

        KeyedSlotEntry entry = Assert.Single(resolved);
        Assert.Equal(SlotGroups.HOTBAR, entry.GroupId);
        Assert.Equal(0, entry.LocalIndex);
        Assert.Equal("laser", entry.Item.ID);
        Assert.Equal(1, entry.Item.Count);
    }

    [Fact]
    public void CreativeResolveGrantsTheFullCreativeLoadout()
    {
        KeyedSlotEntry[] resolved = PlayerInitialInventory.Resolve(keyedSeed: null, legacySeed: null, GameMode.Creative);

        Assert.Equal(PlayerInitialInventory.StarterInventory(GameMode.Creative), resolved);
        Assert.Contains(resolved, entry => entry.GroupId == SlotGroups.HOTBAR && entry.Item.ID == "laser");
        Assert.Contains(resolved, entry => entry.GroupId == SlotGroups.HOTBAR && entry.Item.ID == "wb:sprayer");
        Assert.Contains(resolved, entry => entry.GroupId == SlotGroups.HOTBAR && entry.Item.ID == "wb:drill");
        Assert.Contains(resolved, entry => entry.GroupId == SlotGroups.HOTBAR && entry.Item.ID == "wb:projector");
        Assert.Contains(resolved, entry => entry.GroupId == SlotGroups.BACKPACK && entry.Item.ID == "wb:rock" && entry.Item.Count == 100);
        Assert.True(resolved.Length > 1);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(-1)]
    public void UndefinedGameModeFallsBackToAdventureStarterInventory(int undefinedMode)
    {
        KeyedSlotEntry[] resolved = PlayerInitialInventory.Resolve(keyedSeed: null, legacySeed: null, (GameMode)undefinedMode);

        KeyedSlotEntry entry = Assert.Single(resolved);
        Assert.Equal("laser", entry.Item.ID);
    }

    [Fact]
    public void SavedInventoryIsPreservedInAdventure()
    {
        KeyedSlotEntry[] saved =
        [
            new KeyedSlotEntry(SlotGroups.HOTBAR, 0, new ItemData("wb:rock", 5, 100)),
            new KeyedSlotEntry(SlotGroups.BACKPACK, 3, new ItemData("wb:panel", 2, 100)),
        ];

        KeyedSlotEntry[] resolved = PlayerInitialInventory.Resolve(saved, legacySeed: null, GameMode.Adventure);

        Assert.Equal(saved, resolved);
    }

    [Fact]
    public void EmptySavedInventoryIsPreservedInCreative()
    {
        KeyedSlotEntry[] saved = [];

        KeyedSlotEntry[] resolved = PlayerInitialInventory.Resolve(saved, legacySeed: null, GameMode.Creative);

        Assert.Empty(resolved);
    }

    [Fact]
    public void EmptyLegacyInventoryIsPreservedWithoutRestocking()
    {
        ItemData[] legacySaved = [];

        KeyedSlotEntry[] resolved = PlayerInitialInventory.Resolve(keyedSeed: null, legacySaved, GameMode.Creative);

        Assert.Empty(resolved);
    }
}
