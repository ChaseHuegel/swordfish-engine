using System.Collections.Generic;

namespace WaywardBeyond.Bricks;

/// <summary>TOML schema for optional server block-entity metadata declared under <c>[Bricks.BlockEntity]</c>.</summary>
public struct BlockEntityDefinition()
{
    /// <summary>Optional station tag identifying the machine or container role.</summary>
    public string? StationTag;

    /// <summary>Optional inventory slot groups provisioned when a block-entity spawns for this brick.</summary>
    public List<BrickSlotGroupDefinition>? SlotGroups;

    /// <summary>Nominal power rate in kW; negative draws power and positive generates power.</summary>
    public float PowerKw;

    /// <summary>Whether the brick forms an airtight barrier in its default state.</summary>
    public bool Airtight;

    /// <summary>Whether players or systems can toggle the block-entity state.</summary>
    public bool Toggleable;

    /// <summary>Initial power-enabled state for newly spawned block-entities.</summary>
    public bool DefaultPowerEnabled = true;

    /// <summary>Initial open state for newly spawned toggleable door or hatch block-entities.</summary>
    public bool DefaultOpen;
}
