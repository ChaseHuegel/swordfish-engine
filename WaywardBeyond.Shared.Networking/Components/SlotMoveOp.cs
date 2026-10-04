namespace WaywardBeyond.Shared.Networking.Components;

/// <summary>
/// A slot move operation carried by <see cref="InventoryEvent"/>. <see cref="Count"/> follows the
/// <c>null = whole stack</c> rule: null moves the entire stack from <see cref="FromSlot"/>, a value moves
/// at most that many. Occupied-destination resolution is deterministic server-side (and mirrored by the
/// client's prediction): same item + capacity stacks, otherwise the stacks swap; a partial move onto a
/// different item is a no-op.
/// </summary>
public partial struct SlotMoveOp
{
    /// <summary>Moves an exact amount (whole stack when <see cref="Count"/> is null) to <see cref="ToSlot"/>.</summary>
    public const byte MODE_EXACT = 0;

    /// <summary>Moves the whole stack to the first same-item stack with capacity, else the first empty slot (shift+click).</summary>
    public const byte MODE_AUTO_STACK = 1;
}