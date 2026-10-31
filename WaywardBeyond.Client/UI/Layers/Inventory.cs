using System;
using System.Collections.Generic;
using System.Numerics;
using Reef;
using Reef.UI;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Items;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI.Components;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Items;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Owns the modal inventory window's services, shortcuts, state, and item-move interactions.</summary>
internal sealed class Inventory : IUILayer
{
    private const int SLOTS_PER_ROW = 9;
    private const int SLOT_SIZE = 48;
    private const int SLOT_GAP = 6;

    /// <summary>
    /// The panel height that is not slot rows: the title block, divider spacers, hotbar row, footer,
    /// and panel padding.
    /// </summary>
    private const int PANEL_HEIGHT_CHROME = 185;

    private static readonly string[] _slotNumbers = ["1", "2", "3", "4", "5", "6", "7", "8", "9"];

    /// <summary>The inputs that every slot of one row shares.</summary>
    private readonly record struct SlotRowContext(
        int RowIndex,
        int ActiveSlot,
        bool LeftHeld,
        bool RightPressed,
        bool ShiftHeld,
        float Scroll,
        InventoryComponent Inventory
    );

    private readonly IAssetDatabase<Item> _itemDatabase;
    private readonly IBrickDatabase _brickDatabase;
    private readonly ItemIconDatabase _itemIcons;
    private readonly PlayerData _playerData;
    private readonly IECSContext _ecsContext;
    private readonly InteractionState _interactionState;
    private readonly IInputService _inputService;
    private readonly ILocalization _localization;
    private readonly InventoryView _view;

    private bool _open;
    private int _selectedSlot = -1;
    private InteractionState.InteractionBlocker? _interactionBlocker;

    private bool _dragging;
    private int _draggingSlot;

    /// <summary>Initializes a new instance of the <see cref="Inventory"/> class.</summary>
    public Inventory(
        in IShortcutService shortcutService,
        in IAssetDatabase<Item> itemDatabase,
        in IBrickDatabase brickDatabase,
        in ItemIconDatabase itemIcons,
        in PlayerData playerData,
        in IECSContext ecsContext,
        in InteractionState interactionState,
        in IInputService inputService,
        in ILocalization localization
    )
    {
        _itemDatabase = itemDatabase;
        _brickDatabase = brickDatabase;
        _itemIcons = itemIcons;
        _playerData = playerData;
        _ecsContext = ecsContext;
        _interactionState = interactionState;
        _inputService = inputService;
        _localization = localization;
        _view = new InventoryView(localization);

        var toggleShortcut = new Shortcut
        {
            Name = "Inventory",
            Category = "UI",
            Modifiers = ShortcutModifiers.None,
            Key = Key.Tab,
            IsEnabled = WaywardBeyond.IsPlaying,
            Action = OnToggleInventoryPressed,
        };
        shortcutService.RegisterShortcut(toggleShortcut);

        var closeShortcut = new Shortcut
        {
            Name = "Close",
            Category = "Inventory",
            Modifiers = ShortcutModifiers.None,
            Key = Key.Esc,
            IsEnabled = IsVisible,
            Action = OnCloseInventoryPressed,
        };
        shortcutService.RegisterShortcut(closeShortcut);

        for (var i = 0; i < SLOTS_PER_ROW; i++)
        {
            int slotNumber = i + 1;
            int slotIndex = i;

            var slotShortcut = new Shortcut
            {
                Name = $"Move to slot {slotNumber}",
                Category = "Inventory",
                Modifiers = ShortcutModifiers.None,
                Key = Key.D1 + slotIndex,
                IsEnabled = IsVisible,
                Action = SwapSlots,
            };
            shortcutService.RegisterShortcut(slotShortcut);

            void SwapSlots()
            {
                if (_selectedSlot < 0)
                {
                    return;
                }

                //  Whole-stack move to the hotbar slot; occupied-destination resolution is deterministic
                //  (stack if the same item with capacity, otherwise swap) on both the prediction and the
                //  server.
                ApplyMove(new SlotMoveOp
                {
                    Mode = SlotMoveOpMode.Exact,
                    FromSlot = _selectedSlot,
                    ToSlot = slotIndex,
                    Count = null,
                });
            }
        }
    }

    /// <inheritdoc/>
    public bool IsVisible() => _open && WaywardBeyond.IsPlaying();

    /// <inheritdoc/>
    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        Result<InventoryComponent> inventoryResult = _playerData.GetInventory(_ecsContext.World.DataStore);
        Result<int> activeSlotResult = _playerData.GetActiveSlot(_ecsContext.World.DataStore);
        if (!inventoryResult.Success || !activeSlotResult.Success)
        {
            return Result.FromSuccess();
        }

        InventoryComponent inventory = inventoryResult.Value;
        int activeSlot = activeSlotResult.Value;
        int rowCount = inventory.Contents.Length / SLOTS_PER_ROW;

        bool leftHeld = ui.LeftHeld();
        bool rightPressed = ui.RightPressed();
        bool shiftHeld = _inputService.IsKeyHeld(Key.Shift);
        float scroll = _inputService.GetMouseScroll();

        ItemData dragItemStack = GetSlotStack(in inventory, _draggingSlot);
        ItemData selectedItemStack = GetSlotStack(in inventory, _selectedSlot);
        ResolveDragRelease(leftHeld, in dragItemStack, in selectedItemStack);

        var slotStates = new ItemSlotState[rowCount * SLOTS_PER_ROW];
        var slotRow = new SlotRowContext(
            RowIndex: 0,
            ActiveSlot: activeSlot,
            LeftHeld: leftHeld,
            RightPressed: rightPressed,
            ShiftHeld: shiftHeld,
            Scroll: scroll,
            Inventory: inventory
        );

        Material? draggedSlotIcon = null;
        string? draggedSlotText = null;
        var slotIsSelected = false;

        //  Cargo rows render above the hotbar row, so they process input first.
        for (var rowIndex = 1; rowIndex < rowCount; rowIndex++)
        {
            EvaluateSlotRow(
                ui,
                slotRow with { RowIndex = rowIndex },
                slotStates,
                ref slotIsSelected,
                ref draggedSlotIcon,
                ref draggedSlotText
            );
        }

        if (rowCount > 0)
        {
            EvaluateSlotRow(
                ui,
                slotRow,
                slotStates,
                ref slotIsSelected,
                ref draggedSlotIcon,
                ref draggedSlotText
            );
        }

        if (!slotIsSelected)
        {
            _selectedSlot = -1;
        }

        var cargoRows = new List<InventorySlotRowState>(Math.Max(0, rowCount - 1));
        for (var rowIndex = 1; rowIndex < rowCount; rowIndex++)
        {
            cargoRows.Add(CreateSlotRow(rowIndex, slotStates));
        }

        Vector2 cursorPosition = _inputService.CursorPosition;
        IReadOnlyList<InventorySlotViewState> hotbar = rowCount > 0 ? CreateSlotRow(0, slotStates).Slots : [];
        var state = new InventoryViewState(
            CargoRows: cargoRows,
            Hotbar: hotbar,
            Footer: CreateFooter(in inventory),
            HasDrag: _dragging && draggedSlotIcon != null && draggedSlotText != null,
            DragIcon: draggedSlotIcon!,
            DragCount: draggedSlotText,
            DragX: (int)cursorPosition.X,
            DragY: (int)cursorPosition.Y,
            PanelHeight: CreatePanelHeight(rowCount)
        );

        _view.Render(ui, state);

        return Result.FromSuccess();
    }

    /// <summary>Computes the modal frame height from the cargo row count.</summary>
    private static int CreatePanelHeight(int rowCount)
    {
        int cargoRowCount = Math.Max(0, rowCount - 1);
        int cargoHeight = 0;
        if (cargoRowCount > 0)
        {
            cargoHeight = cargoRowCount * SLOT_SIZE + (cargoRowCount - 1) * SLOT_GAP;
        }

        return cargoHeight + PANEL_HEIGHT_CHROME;
    }

    /// <summary>Builds the view data for one row of slots.</summary>
    private static InventorySlotRowState CreateSlotRow(int rowIndex, ItemSlotState[] slotStates)
    {
        var slots = new InventorySlotViewState[SLOTS_PER_ROW];
        for (var slotIndex = 0; slotIndex < SLOTS_PER_ROW; slotIndex++)
        {
            int inventorySlot = rowIndex * SLOTS_PER_ROW + slotIndex;
            slots[slotIndex] = new InventorySlotViewState(
                $"inventorySlot{inventorySlot}",
                slotStates[inventorySlot]
            );
        }

        return new InventorySlotRowState(slots);
    }

    /// <summary>Resolves the hovered slot's name and its capability badges.</summary>
    private InventoryFooterState CreateFooter(in InventoryComponent inventory)
    {
        ItemData itemStack = GetSlotStack(in inventory, _selectedSlot);
        if (itemStack.Count <= 0 || itemStack.ID == null)
        {
            return new InventoryFooterState(
                Empty: true,
                Unknown: false,
                Name: string.Empty,
                Orientable: false,
                Shapeable: false
            );
        }

        Result<Item> itemResult = _itemDatabase.Get(itemStack.ID);
        if (!itemResult.Success)
        {
            string fallbackName = _localization.GetString(itemStack.ID) ?? itemStack.ID;
            return new InventoryFooterState(
                Empty: false,
                Unknown: true,
                Name: fallbackName,
                Orientable: false,
                Shapeable: false
            );
        }

        Item item = itemResult.Value;
        string itemName = _localization.GetString(item.Name) ?? item.Name;

        bool shapeable = false;
        bool orientable = false;

        if (item.Placeable != null)
        {
            Result<Brick> brickResult = _brickDatabase.Get(item.Placeable.Value.ID);
            if (brickResult.Success)
            {
                Brick brick = brickResult.Value;
                shapeable = brick.Shapeable;
                orientable = brick.Shapeable || brick.IsOrientable(BrickShape.Block);
            }
        }

        return new InventoryFooterState(
            Empty: false,
            Unknown: false,
            Name: itemName,
            Orientable: orientable,
            Shapeable: shapeable
        );
    }

    /// <summary>Completes a released drag by moving the dragged stack onto the selected slot.</summary>
    private void ResolveDragRelease(bool leftHeld, in ItemData dragItemStack, in ItemData selectedItemStack)
    {
        bool isDragSlotEmpty = dragItemStack.Count <= 0;
        if (!_dragging || (leftHeld && !isDragSlotEmpty))
        {
            return;
        }

        _dragging = false;

        if (isDragSlotEmpty || _selectedSlot < 0)
        {
            return;
        }

        //  Same item: fill the selected stack from the dragged stack (partial, client-computed).
        //  Otherwise: whole-stack move, which the resolver stacks-or-swaps deterministically.
        if (dragItemStack.ID == selectedItemStack.ID)
        {
            int available = selectedItemStack.MaxSize - selectedItemStack.Count;
            ApplyMove(new SlotMoveOp
            {
                Mode = SlotMoveOpMode.Exact,
                FromSlot = _draggingSlot,
                ToSlot = _selectedSlot,
                Count = (uint)Math.Max(0, available),
            });
            return;
        }

        ApplyMove(new SlotMoveOp
        {
            Mode = SlotMoveOpMode.Exact,
            FromSlot = _draggingSlot,
            ToSlot = _selectedSlot,
            Count = null,
        });
    }

    /// <summary>Resolves one row of slots' interactions and display state.</summary>
    private void EvaluateSlotRow(
        UIBuilder<Material> ui,
        in SlotRowContext context,
        ItemSlotState[] slotStates,
        ref bool slotIsSelected,
        ref Material? draggedSlotIcon,
        ref string? draggedSlotText
    )
    {
        InventoryComponent inventory = context.Inventory;

        for (var slotIndex = 0; slotIndex < SLOTS_PER_ROW; slotIndex++)
        {
            int inventorySlot = context.RowIndex * SLOTS_PER_ROW + slotIndex;
            ItemData itemStack = GetSlotStack(in inventory, inventorySlot);
            string elementID = $"inventorySlot{inventorySlot}";

            bool hovering = ui.Hovering(elementID);
            bool clicked = ui.Clicked(elementID);
            bool rightClicked = hovering && context.RightPressed;
            bool held = hovering && context.LeftHeld;

            if (!slotIsSelected && hovering)
            {
                _selectedSlot = inventorySlot;
                slotIsSelected = true;
            }

            bool isDraggingSource = _dragging && inventorySlot == _draggingSlot;
            bool isHovered = inventorySlot == _selectedSlot;
            bool isEquippedHotbar = inventorySlot < SLOTS_PER_ROW && inventorySlot == context.ActiveSlot;
            bool hasItem = itemStack.Count > 0 && itemStack.ID != null;

            slotStates[inventorySlot] = new ItemSlotState(
                Icon: hasItem ? _itemIcons.Get(itemStack.ID!) : null!,
                Number: inventorySlot < SLOTS_PER_ROW ? _slotNumbers[slotIndex] : string.Empty,
                Count: hasItem ? itemStack.Count.ToString() : null,
                Equipped: isEquippedHotbar,
                Hovered: isHovered,
                Dragging: isDraggingSource
            );

            ApplyDragGestures(in context, inventorySlot, hovering, rightClicked);

            if (!hasItem)
            {
                continue;
            }

            if (_dragging && _draggingSlot == inventorySlot)
            {
                draggedSlotIcon = slotStates[inventorySlot].Icon;
                draggedSlotText = slotStates[inventorySlot].Count;
            }

            ApplyClickInteraction(in context, inventorySlot, clicked, held, rightClicked, in itemStack);
        }
    }

    /// <summary>Applies the right-click and scroll gestures that change a dragged stack.</summary>
    private void ApplyDragGestures(in SlotRowContext context, int inventorySlot, bool hovering, bool rightClicked)
    {
        if (!_dragging)
        {
            return;
        }

        //  Right-clicking a slot while dragging an item drops 1 count
        if (rightClicked)
        {
            ApplyMove(new SlotMoveOp
            {
                Mode = SlotMoveOpMode.Exact,
                FromSlot = _draggingSlot,
                ToSlot = inventorySlot,
                Count = 1,
            });
        }

        //  Scrolling while dragging an item will add/remove items by 1
        if (!hovering || context.Scroll == 0f)
        {
            return;
        }

        int sourceSlot = _draggingSlot;
        int destinationSlot = inventorySlot;
        if (context.Scroll < 0f)
        {
            sourceSlot = inventorySlot;
            destinationSlot = _draggingSlot;
        }

        var amount = (int)Math.Abs(context.Scroll);
        ApplyMove(new SlotMoveOp
        {
            Mode = SlotMoveOpMode.Exact,
            FromSlot = sourceSlot,
            ToSlot = destinationSlot,
            Count = (uint)amount,
        });
    }

    /// <summary>Applies the click gestures that move, split, or pick up a stack.</summary>
    private void ApplyClickInteraction(
        in SlotRowContext context,
        int inventorySlot,
        bool clicked,
        bool held,
        bool rightClicked,
        in ItemData itemStack
    )
    {
        if (context.ShiftHeld)
        {
            //  Shift + click and shift + hold left click quick moves items via the
            //  server-resolved AutoStack op.
            if (!clicked && !held)
            {
                return;
            }

            ApplyMove(new SlotMoveOp
            {
                Mode = SlotMoveOpMode.AutoStack,
                FromSlot = inventorySlot,
                ToSlot = -1,
                Count = null,
            });
            return;
        }

        //  Right-clicking a slot splits the stack: move half to the first
        //  empty slot (client-computed target, server revalidated).
        if (!_dragging && rightClicked)
        {
            ApplySplitMove(in context, inventorySlot, in itemStack);
        }

        //  Start dragging this slot
        if (!_dragging && held)
        {
            _dragging = true;
            _draggingSlot = inventorySlot;
        }
    }

    /// <summary>Moves half of a stack into the first empty slot.</summary>
    private void ApplySplitMove(in SlotRowContext context, int inventorySlot, in ItemData itemStack)
    {
        InventoryComponent inventory = context.Inventory;
        int splitTarget = InventoryResolver.FindFirstEmptySlot(in inventory);
        if (splitTarget < 0)
        {
            return;
        }

        ApplyMove(new SlotMoveOp
        {
            Mode = SlotMoveOpMode.Exact,
            FromSlot = inventorySlot,
            ToSlot = splitTarget,
            Count = (uint)Math.Max(1, itemStack.Count / 2),
        });
    }

    private static ItemData GetSlotStack(in InventoryComponent inventory, int slotIndex)
    {
        if (inventory.Contents != null && slotIndex >= 0 && slotIndex < inventory.Contents.Length)
        {
            return inventory.Contents[slotIndex];
        }

        return default;
    }

    /// <summary>
    /// Applies a move op to the local inventory as prediction and stages it for upstream replication,
    /// through the shared <see cref="PlayerData.ApplyMove"/> entry point.
    /// </summary>
    private void ApplyMove(in SlotMoveOp moveOp)
    {
        _playerData.ApplyMove(_ecsContext.World.DataStore, moveOp);
    }

    private void OnToggleInventoryPressed()
    {
        if (_open)
        {
            _interactionBlocker?.Dispose();
            _interactionBlocker = null;
            _open = false;
            return;
        }

        if (!_interactionState.TryBlockInteractionExclusive(out _interactionBlocker))
        {
            return;
        }

        _open = true;
    }

    private void OnCloseInventoryPressed()
    {
        _interactionBlocker?.Dispose();
        _interactionBlocker = null;
        _open = false;
    }
}
