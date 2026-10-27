using Swordfish.Graphics;

namespace WaywardBeyond.Client.UI.Components;

/// <summary>Data for one inventory or hotbar slot.</summary>
internal readonly record struct ItemSlotState(
    Material Icon,
    string Number,
    string? Count = null,
    bool Selected = false,
    bool Equipped = false,
    bool Hovered = false,
    bool Dragging = false
)
{
    /// <summary>Gets whether the slot shows an item icon.</summary>
    public bool HasItem => Icon != null;

    /// <summary>Gets whether the slot shows a count badge.</summary>
    public bool HasCount => Count != null;

    /// <summary>Gets whether the slot shows a slot number.</summary>
    public bool HasNumber => !string.IsNullOrEmpty(Number);

    /// <summary>Gets whether the slot number uses the active tint.</summary>
    public bool NumberActive => Selected || Equipped;
}
