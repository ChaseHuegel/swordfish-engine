using System.Collections.Generic;

namespace WaywardBeyond.Bricks;

/// <summary>TOML schema for optional brick-entity metadata declared under <c>[Bricks.Entity]</c>.</summary>
public struct BrickEntityDefinition()
{
    /// <summary>Optional point-light configuration declared under <c>[Bricks.Entity.Light]</c>.</summary>
    public BrickLightDefinition? Light;

    /// <summary>Optional inventory slot groups declared under <c>[[Bricks.Entity.SlotGroups]]</c>.</summary>
    public List<BrickSlotGroupDefinition>? SlotGroups;
}
