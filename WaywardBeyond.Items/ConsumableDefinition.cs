using Tomlet.Attributes;

namespace WaywardBeyond.Items;

/// <summary>Defines the survival effect of consuming one item.</summary>
public struct ConsumableDefinition()
{
    /// <summary>Health restored on use, in health points.</summary>
    [TomlField("heal_hp")]
    public float HealHp;

    /// <summary>Oxygen restored on use, in oxygen points.</summary>
    [TomlField("restore_o2")]
    public float RestoreO2;

    /// <summary>Optional buff granted on use.</summary>
    [TomlField("grant_buff_id")]
    public string? GrantBuffID;

    /// <summary>The minimum seconds between uses of the item.</summary>
    [TomlField("cooldown_seconds")]
    public float CooldownSeconds;
}
