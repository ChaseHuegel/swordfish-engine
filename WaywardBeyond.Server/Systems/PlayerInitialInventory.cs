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
    /// Resolves sparse keyed entries for a joining character from its saved keyed or legacy contents,
    /// granting the starter set for <paramref name="gameMode"/> only when both are <c>null</c>.
    /// </summary>
    public static KeyedSlotEntry[] Resolve(KeyedSlotEntry[]? keyedSeed, ItemData[]? legacySeed, GameMode gameMode)
    {
        if (keyedSeed != null)
        {
            return keyedSeed;
        }

        if (legacySeed != null)
        {
            return KeyedInventoryMigration.FromLegacySlots(legacySeed) ?? [];
        }

        return StarterInventory(gameMode);
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
        ItemData[] positional =
        [
            new ItemData("laser", 1, 1),
            new ItemData("wb:sprayer", 1, 1),
            new ItemData("wb:drill", 1, 1),
            new ItemData("wb:projector", 1, 1),
            new ItemData("wb:panel", 100, 100),
            new ItemData("wb:thruster", 100, 100),
            new ItemData("wb:display_control", 100, 100),
            new ItemData("wb:caution_panel", 100, 100),
            new ItemData("wb:glass", 100, 100),
            new ItemData("wb:display_monitor", 100, 100),
            new ItemData("wb:storage", 100, 100),
            new ItemData("wb:truss", 100, 100),
            new ItemData("wb:small_light", 100, 100),
            new ItemData("wb:light", 100, 100),
            new ItemData("wb:display_console", 100, 100),
            new ItemData("wb:ice", 100, 100),
            new ItemData("wb:rock", 100, 100),
            new ItemData("wb:control_buttons", 100, 100),
            new ItemData("wb:grate", 100, 100),
            new ItemData("wb:core", 100, 100),
            new ItemData("wb:porthole", 100, 100),
            new ItemData("wb:vent", 100, 100),
            new ItemData("wb:control_panel", 100, 100),
        ];
        return KeyedInventoryMigration.FromLegacySlots(positional) ?? [];
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
