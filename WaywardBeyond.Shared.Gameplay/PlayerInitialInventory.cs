using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Shared.Gameplay;

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
            InventoryComponent.Stack("panel", 100, 100),
            InventoryComponent.Stack("thruster", 100, 100),
            InventoryComponent.Stack("display_control", 100, 100),
            InventoryComponent.Stack("caution_panel", 100, 100),
            InventoryComponent.Stack("glass", 100, 100),
            InventoryComponent.Stack("display_monitor", 100, 100),
            InventoryComponent.Stack("storage", 100, 100),
            InventoryComponent.Stack("truss", 100, 100),
            InventoryComponent.Stack("small_light", 100, 100),
            InventoryComponent.Stack("light", 100, 100),
            InventoryComponent.Stack("display_console", 100, 100),
            InventoryComponent.Stack("ice", 100, 100),
            InventoryComponent.Stack("rock", 100, 100),
            InventoryComponent.Stack("control_buttons", 100, 100),
            InventoryComponent.Stack("grate", 100, 100),
            InventoryComponent.Stack("core", 100, 100),
            InventoryComponent.Stack("porthole", 100, 100),
            InventoryComponent.Stack("vent", 100, 100),
            InventoryComponent.Stack("control_panel", 100, 100),
        ];
    }
}