#nullable enable
using WaywardBeyond.Data;
using WaywardBeyond.Server.Systems;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Pins the starter inventory rules in <see cref="PlayerInitialInventory"/>.</summary>
public class PlayerInitialInventoryTests
{
    [Fact]
    public void AdventureStarterInventoryGrantsOnlyTheMiningLaser()
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
        KeyedSlotEntry[] resolved = PlayerInitialInventory.Resolve(seed: null, GameMode.Adventure);

        KeyedSlotEntry entry = Assert.Single(resolved);
        Assert.Equal(SlotGroups.HOTBAR, entry.GroupId);
        Assert.Equal(0, entry.LocalIndex);
        Assert.Equal("laser", entry.Item.ID);
        Assert.Equal(1, entry.Item.Count);
    }

    [Fact]
    public void CreativeResolveGrantsTheFullCreativeLoadout()
    {
        KeyedSlotEntry[] resolved = PlayerInitialInventory.Resolve(seed: null, GameMode.Creative);

        Assert.Equal(PlayerInitialInventory.StarterInventory(GameMode.Creative), resolved);
        Assert.Contains(resolved, entry => entry.GroupId == SlotGroups.HOTBAR && entry.Item.ID == "laser");
        Assert.Contains(resolved, entry => entry.GroupId == SlotGroups.HOTBAR && entry.Item.ID == "wb:sprayer");
        Assert.Contains(resolved, entry => entry.GroupId == SlotGroups.HOTBAR && entry.Item.ID == "wb:drill");
        Assert.Contains(resolved, entry => entry.GroupId == SlotGroups.HOTBAR && entry.Item.ID == "wb:projector");
        Assert.Contains(resolved, entry => entry.GroupId == SlotGroups.BACKPACK && entry.Item.ID == "wb:rock" && entry.Item.Count == 100);
        Assert.True(resolved.Length > 1);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(-1)]
    public void UndefinedGameModeFallsBackToAdventureStarterInventory(int undefinedMode)
    {
        KeyedSlotEntry[] resolved = PlayerInitialInventory.Resolve(seed: null, (GameMode)undefinedMode);

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

        KeyedSlotEntry[] resolved = PlayerInitialInventory.Resolve(saved, GameMode.Adventure);

        Assert.Equal(saved, resolved);
    }

    [Fact]
    public void EmptySavedInventoryIsPreservedInCreative()
    {
        KeyedSlotEntry[] saved = [];

        KeyedSlotEntry[] resolved = PlayerInitialInventory.Resolve(saved, GameMode.Creative);

        Assert.Empty(resolved);
    }
}
