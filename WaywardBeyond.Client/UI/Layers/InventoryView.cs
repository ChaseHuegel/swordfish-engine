using System.Collections.Generic;
using Reef.Constraints;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using WaywardBeyond.Client.UI.Components;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Display data for one inventory slot: its key-bound interaction id plus its visuals.</summary>
internal readonly record struct InventorySlotViewState(string ID, ItemSlotState Slot);

/// <summary>Display data for one horizontal row of inventory slots.</summary>
internal readonly record struct InventorySlotRowState(IReadOnlyList<InventorySlotViewState> Slots);

/// <summary>Display data for the item inspection footer.</summary>
internal readonly record struct InventoryFooterState(
    bool Empty,
    bool Unknown,
    string Name,
    bool Orientable,
    bool Shapeable
);

/// <summary>Display data for the modal inventory frame and its drag preview.</summary>
internal readonly record struct InventoryViewState(
    IReadOnlyList<InventorySlotRowState> CargoRows,
    IReadOnlyList<InventorySlotViewState> Hotbar,
    InventoryFooterState Footer,
    bool HasDrag,
    Material DragIcon,
    string? DragCount,
    int DragX,
    int DragY,
    int PanelHeight
)
{
    /// <summary>Gets the modal frame height constraint.</summary>
    public IConstraint PanelHeightConstraint => new Fixed(PanelHeight);
}

/// <summary>Draws the modal inventory window, its slot rows, and the dragged stack preview.</summary>
internal partial class InventoryView(ILocalization localization) : UiViewBase(localization)
{
}
