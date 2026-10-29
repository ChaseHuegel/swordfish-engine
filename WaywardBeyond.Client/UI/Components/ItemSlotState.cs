using Swordfish.Graphics;

namespace WaywardBeyond.Client.UI.Components;

/// <summary>Data for one inventory or hotbar slot.</summary>
internal readonly record struct ItemSlotState(
    Material Icon,
    string Number,
    string? Count = null,
    bool Selected = false,
    bool Equipped = false
)
{
    /// <summary>Gets whether the slot shows a count badge.</summary>
    public bool HasCount => Count != null;
}
