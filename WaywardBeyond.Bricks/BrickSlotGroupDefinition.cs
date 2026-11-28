namespace WaywardBeyond.Bricks;

/// <summary>TOML schema for one inventory slot group declared under <c>[[Bricks.BlockEntity.SlotGroups]]</c>.</summary>
public struct BrickSlotGroupDefinition()
{
    /// <summary>The namespaced slot group identifier (for example <c>wb:cargo</c> or <c>wb:input</c>).</summary>
    public string ID = string.Empty;

    /// <summary>Optional slot capacity override; omitted uses the registered slot group schema capacity.</summary>
    public int? Capacity;
}
