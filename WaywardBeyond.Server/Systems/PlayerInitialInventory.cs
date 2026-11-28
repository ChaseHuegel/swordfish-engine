using WaywardBeyond.Data;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Resolves a character's effective starting inventory. The server is the sole authority for granting
/// starter inventory: a client-authored new character seeds <c>null</c> contents, and the server grants
/// the starter set for the world's game mode. A saved inventory is used as-is (even if empty) — it is
/// never restocked.
/// </summary>
public static class PlayerInitialInventory
{
    /// <summary>
    /// Resolves sparse keyed entries for a joining character from its saved contents, granting the
    /// starter set for <paramref name="gameMode"/> only when <paramref name="seed"/> is <c>null</c>.
    /// </summary>
    public static KeyedSlotEntry[] Resolve(KeyedSlotEntry[]? seed, GameMode gameMode)
    {
        return seed ?? StarterInventory(gameMode);
    }

    /// <summary>The starter loadout granted to a character with no saved inventory in <paramref name="gameMode"/>.</summary>
    public static KeyedSlotEntry[] StarterInventory(GameMode gameMode)
    {
        return gameMode == GameMode.Creative
            ? CreativeStarterInventory()
            : AdventureStarterInventory();
    }

    /// <summary>The creative starter loadout: the core tools in the hotbar plus building blocks in the backpack.</summary>
    private static KeyedSlotEntry[] CreativeStarterInventory()
    {
        return
        [
            new KeyedSlotEntry(SlotGroups.HOTBAR, 0, new ItemData("laser", 1, 1)),
            new KeyedSlotEntry(SlotGroups.HOTBAR, 1, new ItemData("wb:sprayer", 1, 1)),
            new KeyedSlotEntry(SlotGroups.HOTBAR, 2, new ItemData("wb:drill", 1, 1)),
            new KeyedSlotEntry(SlotGroups.HOTBAR, 3, new ItemData("wb:projector", 1, 1)),
            new KeyedSlotEntry(SlotGroups.HOTBAR, 4, new ItemData("wb:panel", 100, 100)),
            new KeyedSlotEntry(SlotGroups.HOTBAR, 5, new ItemData("wb:thruster", 100, 100)),
            new KeyedSlotEntry(SlotGroups.HOTBAR, 6, new ItemData("wb:display_control", 100, 100)),
            new KeyedSlotEntry(SlotGroups.HOTBAR, 7, new ItemData("wb:caution_panel", 100, 100)),
            new KeyedSlotEntry(SlotGroups.HOTBAR, 8, new ItemData("wb:glass", 100, 100)),
            new KeyedSlotEntry(SlotGroups.BACKPACK, 0, new ItemData("wb:display_monitor", 100, 100)),
            new KeyedSlotEntry(SlotGroups.BACKPACK, 1, new ItemData("wb:storage", 100, 100)),
            new KeyedSlotEntry(SlotGroups.BACKPACK, 2, new ItemData("wb:truss", 100, 100)),
            new KeyedSlotEntry(SlotGroups.BACKPACK, 3, new ItemData("wb:small_light", 100, 100)),
            new KeyedSlotEntry(SlotGroups.BACKPACK, 4, new ItemData("wb:light", 100, 100)),
            new KeyedSlotEntry(SlotGroups.BACKPACK, 5, new ItemData("wb:display_console", 100, 100)),
            new KeyedSlotEntry(SlotGroups.BACKPACK, 6, new ItemData("wb:ice", 100, 100)),
            new KeyedSlotEntry(SlotGroups.BACKPACK, 7, new ItemData("wb:rock", 100, 100)),
            new KeyedSlotEntry(SlotGroups.BACKPACK, 8, new ItemData("wb:control_buttons", 100, 100)),
            new KeyedSlotEntry(SlotGroups.BACKPACK, 9, new ItemData("wb:grate", 100, 100)),
            new KeyedSlotEntry(SlotGroups.BACKPACK, 10, new ItemData("wb:core", 100, 100)),
            new KeyedSlotEntry(SlotGroups.BACKPACK, 11, new ItemData("wb:porthole", 100, 100)),
            new KeyedSlotEntry(SlotGroups.BACKPACK, 12, new ItemData("wb:vent", 100, 100)),
            new KeyedSlotEntry(SlotGroups.BACKPACK, 13, new ItemData("wb:control_panel", 100, 100)),
        ];
    }

    /// <summary>The adventure starter loadout: only the mining laser.</summary>
    private static KeyedSlotEntry[] AdventureStarterInventory()
    {
        return
        [
            new KeyedSlotEntry(SlotGroups.HOTBAR, 0, new ItemData("laser", 1, 1)),
        ];
    }
}
