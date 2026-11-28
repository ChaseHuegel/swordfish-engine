using System.Collections.Generic;

namespace WaywardBeyond.Bricks;

/// <summary>Validated brick-entity metadata bound to a <see cref="Brick"/>.</summary>
public sealed class BrickEntity(
    BrickLight? light = null,
    BrickSlotGroup[]? slotGroups = null
)
{
    private readonly BrickSlotGroup[] _slotGroups = slotGroups ?? [];

    /// <summary>Optional point-light configuration for this brick entity.</summary>
    public BrickLight? Light { get; } = light;

    /// <summary>The ordered slot group declarations provisioned on the brick-entity inventory.</summary>
    public IReadOnlyList<BrickSlotGroup> SlotGroups => _slotGroups;

    /// <summary>Whether this brick entity provisions an inventory component.</summary>
    public bool HasInventory => _slotGroups.Length > 0;
}
