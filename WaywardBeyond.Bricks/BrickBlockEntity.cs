using System.Collections.Generic;

namespace WaywardBeyond.Bricks;

/// <summary>Validated functional block-entity metadata bound to a <see cref="Brick"/>.</summary>
public sealed class BrickBlockEntity(
    string stationTag,
    BrickSlotGroup[] slotGroups,
    float powerKw = 0f,
    bool airtight = false,
    bool toggleable = false,
    bool defaultPowerEnabled = true,
    bool defaultOpen = false
)
{
    private readonly BrickSlotGroup[] _slotGroups = slotGroups ?? [];

    /// <summary>The station tag identifying this functional brick, or empty when untagged.</summary>
    public string StationTag { get; } = stationTag ?? string.Empty;

    /// <summary>The ordered slot group declarations provisioned on the block-entity inventory.</summary>
    public IReadOnlyList<BrickSlotGroup> SlotGroups => _slotGroups;

    /// <summary>Whether this functional brick provisions an inventory component.</summary>
    public bool HasInventory => _slotGroups.Length > 0;

    /// <summary>Nominal power rate in kW; negative draws power and positive generates power.</summary>
    public float PowerKw { get; } = powerKw;

    /// <summary>Whether the brick is airtight in its default closed state.</summary>
    public bool Airtight { get; } = airtight;

    /// <summary>Whether players or systems can toggle this block-entity.</summary>
    public bool Toggleable { get; } = toggleable;

    /// <summary>Initial power-enabled state for newly placed block-entities.</summary>
    public bool DefaultPowerEnabled { get; } = defaultPowerEnabled;

    /// <summary>Initial open state for newly placed door or hatch block-entities.</summary>
    public bool DefaultOpen { get; } = defaultOpen;
}
