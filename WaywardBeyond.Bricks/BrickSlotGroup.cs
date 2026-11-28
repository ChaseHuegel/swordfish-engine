namespace WaywardBeyond.Bricks;

/// <summary>Validated inventory slot group entry configured on a functional brick.</summary>
public readonly record struct BrickSlotGroup(string GroupId, int? Capacity);
