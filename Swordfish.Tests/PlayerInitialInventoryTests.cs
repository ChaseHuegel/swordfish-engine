using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking.Components;
using Xunit;

namespace Swordfish.Tests;

public class PlayerInitialInventoryTests
{
    [Fact]
    public void ResolveNullSeedGrantsStarterInventory()
    {
        ItemData[] resolved = PlayerInitialInventory.Resolve(null);

        Assert.NotNull(resolved);
        Assert.Equal(20, resolved.Length);
        Assert.Contains(resolved, stack => stack.ID == "laser" && stack.Count == 1 && stack.MaxSize == 1);
        Assert.Contains(resolved, stack => stack.ID == "panel" && stack.Count == 100);
        Assert.Contains(resolved, stack => stack.ID == "control_panel" && stack.Count == 100);
    }

    [Fact]
    public void ResolveSavedSeedReturnsSavedContentsUnchanged()
    {
        ItemData[] saved =
        [
            InventoryComponent.Stack("rock", 3, 100),
            InventoryComponent.Stack("glass", 2, 100),
        ];

        ItemData[] resolved = PlayerInitialInventory.Resolve(saved);

        Assert.Same(saved, resolved);
        Assert.Equal(2, resolved.Length);
        Assert.Equal("rock", resolved[0].ID);
        Assert.Equal("glass", resolved[1].ID);
    }

    [Fact]
    public void ResolveEmptySavedSeedIsNotRestocked()
    {
        //  An empty-but-present inventory array is a player who cleared their inventory - it must NOT
        //  be treated as a new character and restocked.
        ItemData[] resolved = PlayerInitialInventory.Resolve([]);

        Assert.NotNull(resolved);
        Assert.Empty(resolved);
    }

    [Fact]
    public void CharacterSeedNullableInventoryRoundTrips()
    {
        var seed = new CharacterSeed
        {
            CharacterId = 1,
            Name = "Ada",
            Body = 3,
            InventoryContents = null,
            ActiveInventorySlot = 0,
            GameMode = (int)GameMode.Creative,
        };

        byte[] bytes = seed.Serialize();
        CharacterSeed roundTripped = CharacterSeed.Deserialize(bytes);

        Assert.Null(roundTripped.InventoryContents);
    }

    [Fact]
    public void StarterInventoryFitsCanonicalInventory()
    {
        var inventory = new InventoryComponent();
        inventory.CopyFrom(PlayerInitialInventory.StarterInventory());

        Assert.Equal(InventoryComponent.DefaultSlotCount, inventory.Contents.Length);
        Assert.Equal("laser", inventory.Contents[0].ID);
        Assert.Equal("control_panel", inventory.Contents[19].ID);
        Assert.Null(inventory.Contents[20].ID);
    }
}