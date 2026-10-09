using WaywardBeyond.Data;
using WaywardBeyond.Networking.Components;

namespace WaywardBeyond.Gameplay;

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
            InventoryComponent.Stack("laser", 1, 1),
            InventoryComponent.Stack("wb:panel", 100, 100),
            InventoryComponent.Stack("wb:thruster", 100, 100),
            InventoryComponent.Stack("wb:display_control", 100, 100),
            InventoryComponent.Stack("wb:caution_panel", 100, 100),
            InventoryComponent.Stack("wb:glass", 100, 100),
            InventoryComponent.Stack("wb:display_monitor", 100, 100),
            InventoryComponent.Stack("wb:storage", 100, 100),
            InventoryComponent.Stack("wb:truss", 100, 100),
            InventoryComponent.Stack("wb:small_light", 100, 100),
            InventoryComponent.Stack("wb:light", 100, 100),
            InventoryComponent.Stack("wb:display_console", 100, 100),
            InventoryComponent.Stack("wb:ice", 100, 100),
            InventoryComponent.Stack("wb:rock", 100, 100),
            InventoryComponent.Stack("wb:control_buttons", 100, 100),
            InventoryComponent.Stack("wb:grate", 100, 100),
            InventoryComponent.Stack("wb:core", 100, 100),
            InventoryComponent.Stack("wb:porthole", 100, 100),
            InventoryComponent.Stack("wb:vent", 100, 100),
            InventoryComponent.Stack("wb:control_panel", 100, 100),
        ];
    }
}