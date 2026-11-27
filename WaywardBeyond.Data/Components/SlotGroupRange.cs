namespace WaywardBeyond.Data.Components;

/// <summary>A contiguous slice assigned to one slot group inside <see cref="SlotGroupLayout"/>.</summary>
public readonly record struct SlotGroupRange(string GroupId, int Offset, int Capacity);
