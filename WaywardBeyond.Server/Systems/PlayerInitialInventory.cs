using WaywardBeyond.Data;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Resolves a character's effective starting inventory. The server is the sole authority for granting
/// starter inventory: a client-authored new character seeds <c>null</c> contents, and the server grants
/// the starter set. A saved inventory is used as-is (even if empty) — it is never restocked.
/// </summary>
public static class PlayerInitialInventory
{
    /// <summary>
    /// Resolves the inventory the server should grant/seeded for a joining character from its seed
    /// contents: the saved contents when non-null, otherwise the starter set.
    /// </summary>
    public static ItemData[] Resolve(ItemData[]? seedContents)
    {
        if (seedContents != null)
        {
            return seedContents;
        }

        return StarterInventory();
    }

    /// <summary>The starter loadout granted to a character with no saved inventory.</summary>
    public static ItemData[] StarterInventory()
    {
        return
        [
            new ItemData("laser", 1, 1),
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
    }
}