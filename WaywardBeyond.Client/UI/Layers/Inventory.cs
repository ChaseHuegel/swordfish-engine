using System;
using System.Collections.Generic;
using System.Numerics;
using Reef;
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
        bool LeftPressed,
        bool LeftHeld,
        bool RightPressed,
        bool RightDown,
        bool ShiftHeld,
        bool HandledDragClick,
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
    private int _draggingSlot = -1;
    private bool _rightDistributing;
    private int _rightOriginSlot = -1;
    private bool _rightLeftOrigin;
    private int _distributedSlotCount;
    private int _distributedRemainder;
    private readonly int[] _distributedSlots = new int[45];
    private readonly int[] _distributedAllocations = new int[45];

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

                ResetDragState();

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

        bool leftPressed = ui.LeftPressed();
        bool leftHeld = ui.LeftHeld();
        bool rightPressed = ui.RightPressed();
        bool rightDown = rightPressed || ui.RightHeld();
        bool shiftHeld = _inputService.IsKeyHeld(Key.Shift);
        float scroll = _inputService.GetMouseScroll();

        ItemData dragItemStack = GetSlotStack(in inventory, _draggingSlot);
        if (_dragging && dragItemStack.Count <= 0)
        {
            ResetDragState();
        }

        if (_dragging && rightPressed && !_rightDistributing)
        {
            _rightDistributing = true;
            _rightOriginSlot = _selectedSlot;
            _rightLeftOrigin = false;
            _distributedSlotCount = 0;
        }

        if (_rightDistributing && !rightDown)
        {
            CommitRightDistribution(in inventory);
            inventoryResult = _playerData.GetInventory(_ecsContext.World.DataStore);
            if (!inventoryResult.Success)
            {
                return Result.FromSuccess();
            }

            inventory = inventoryResult.Value;
            dragItemStack = GetSlotStack(in inventory, _draggingSlot);
            if (dragItemStack.Count <= 0)
            {
                ResetDragState();
            }
        }

        ItemData selectedItemStack = GetSlotStack(in inventory, _selectedSlot);
        bool handledDragClick = ResolveDragClick(leftPressed, in dragItemStack, in selectedItemStack);

        if (_rightDistributing)
        {
            RecomputeDistribution(in inventory);
        }

        var slotStates = new ItemSlotState[rowCount * SLOTS_PER_ROW];
        var slotRow = new SlotRowContext(
            RowIndex: 0,
            ActiveSlot: activeSlot,
            LeftPressed: leftPressed,
            LeftHeld: leftHeld,
            RightPressed: rightPressed,
            RightDown: rightDown,
            ShiftHeld: shiftHeld,
            HandledDragClick: handledDragClick,
            Scroll: scroll,
            Inventory: inventory
        );

        var slotIsSelected = false;
        int prevDistributedCount = _distributedSlotCount;

        //  Cargo rows render above the hotbar row, so they process input first.
        for (var rowIndex = 1; rowIndex < rowCount; rowIndex++)
        {
            EvaluateSlotRow(
                ui,
                slotRow with { RowIndex = rowIndex },
                slotStates,
                ref slotIsSelected
            );
        }

        if (rowCount > 0)
        {
            EvaluateSlotRow(
                ui,
                slotRow,
                slotStates,
                ref slotIsSelected
            );
        }

        if (_rightDistributing && _distributedSlotCount != prevDistributedCount)
        {
            RefreshDistributedSlotStates(in inventory, activeSlot, slotStates);
        }

        if (!slotIsSelected)
        {
            _selectedSlot = -1;
        }

        Material? draggedSlotIcon = null;
        string? draggedSlotText = null;
        if (_dragging && dragItemStack.Count > 0 && dragItemStack.ID != null)
        {
            draggedSlotIcon = _itemIcons.Get(dragItemStack.ID);
            draggedSlotText = _rightDistributing ? _distributedRemainder.ToString() : dragItemStack.Count.ToString();
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

    /// <summary>Completes a click-to-drop by moving the picked-up stack onto the selected slot or putting it down.</summary>
    private bool ResolveDragClick(bool leftPressed, in ItemData dragItemStack, in ItemData selectedItemStack)
    {
        if (!_dragging || !leftPressed || _rightDistributing)
        {
            return false;
        }

        int fromSlot = _draggingSlot;
        ResetDragState();

        if (dragItemStack.Count <= 0 || _selectedSlot < 0 || _selectedSlot == fromSlot)
        {
            return true;
        }

        //  Same item: fill the selected stack from the dragged stack (partial, client-computed).
        //  Otherwise: whole-stack move, which the resolver stacks-or-swaps deterministically.
        if (dragItemStack.ID == selectedItemStack.ID)
        {
            int available = selectedItemStack.MaxSize - selectedItemStack.Count;
            ApplyMove(new SlotMoveOp
            {
                Mode = SlotMoveOpMode.Exact,
                FromSlot = fromSlot,
                ToSlot = _selectedSlot,
                Count = (uint)Math.Max(0, available),
            });
            return true;
        }

        ApplyMove(new SlotMoveOp
        {
            Mode = SlotMoveOpMode.Exact,
            FromSlot = fromSlot,
            ToSlot = _selectedSlot,
            Count = null,
        });
        return true;
    }

    /// <summary>Commits an active right-click drag distribution or single-slot drop on release.</summary>
    private void CommitRightDistribution(in InventoryComponent inventory)
    {
        RecomputeDistribution(in inventory);

        if (_distributedSlotCount == 1 && !_rightLeftOrigin)
        {
            int targetSlot = _distributedSlots[0];
            if (targetSlot != _draggingSlot)
            {
                ApplyMove(new SlotMoveOp
                {
                    Mode = SlotMoveOpMode.Exact,
                    FromSlot = _draggingSlot,
                    ToSlot = targetSlot,
                    Count = 1,
                });
            }
        }
        else if (_distributedSlotCount > 0)
        {
            for (var i = 0; i < _distributedSlotCount; i++)
            {
                int targetSlot = _distributedSlots[i];
                int share = _distributedAllocations[i];
                if (targetSlot == _draggingSlot || share <= 0)
                {
                    continue;
                }

                ApplyMove(new SlotMoveOp
                {
                    Mode = SlotMoveOpMode.Exact,
                    FromSlot = _draggingSlot,
                    ToSlot = targetSlot,
                    Count = (uint)share,
                });
            }
        }

        _rightDistributing = false;
        _rightOriginSlot = -1;
        _rightLeftOrigin = false;
        _distributedSlotCount = 0;
        _distributedRemainder = 0;
    }

    private void RecomputeDistribution(in InventoryComponent inventory)
    {
        ReadOnlySpan<int> targets = _distributedSlots.AsSpan(0, _distributedSlotCount);
        Span<int> allocations = _distributedAllocations.AsSpan(0, _distributedSlotCount);
        _distributedRemainder = InventoryResolver.ComputeDistribution(in inventory, _draggingSlot, targets, allocations);
    }

    private void TryAddDistributedSlot(in InventoryComponent inventory, in ItemData dragItemStack, int slotIndex)
    {
        if (!_rightDistributing || dragItemStack.Count <= 0 || _distributedSlotCount >= dragItemStack.Count)
        {
            return;
        }

        if (_distributedSlotCount >= _distributedSlots.Length)
        {
            return;
        }

        if (!InventoryResolver.CanDistributeToSlot(in inventory, _draggingSlot, slotIndex))
        {
            return;
        }

        for (var i = 0; i < _distributedSlotCount; i++)
        {
            if (_distributedSlots[i] == slotIndex)
            {
                return;
            }
        }

        _distributedSlots[_distributedSlotCount++] = slotIndex;
        RecomputeDistribution(in inventory);
    }

    private int GetDistributedShare(int slotIndex)
    {
        if (!_rightDistributing)
        {
            return 0;
        }

        for (var i = 0; i < _distributedSlotCount; i++)
        {
            if (_distributedSlots[i] == slotIndex)
            {
                return _distributedAllocations[i];
            }
        }

        return 0;
    }

    private void ResetDragState()
    {
        _dragging = false;
        _draggingSlot = -1;
        _rightDistributing = false;
        _rightOriginSlot = -1;
        _rightLeftOrigin = false;
        _distributedSlotCount = 0;
        _distributedRemainder = 0;
    }

    /// <summary>Resolves one row of slots' interactions and display state.</summary>
    private void EvaluateSlotRow(
        UIBuilder<Material> ui,
        in SlotRowContext context,
        ItemSlotState[] slotStates,
        ref bool slotIsSelected
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

            if (_rightDistributing && hovering && context.RightDown)
            {
                if (inventorySlot != _rightOriginSlot)
                {
                    _rightLeftOrigin = true;
                }

                ItemData dragItemStack = GetSlotStack(in inventory, _draggingSlot);
                TryAddDistributedSlot(in inventory, in dragItemStack, inventorySlot);
            }

            int distributedShare = GetDistributedShare(inventorySlot);
            bool isDistributedSlot = _rightDistributing && distributedShare > 0;
            bool isDraggingSource = _dragging && inventorySlot == _draggingSlot;
            bool isHovered = inventorySlot == _selectedSlot;
            bool isEquippedHotbar = inventorySlot < SLOTS_PER_ROW && inventorySlot == context.ActiveSlot;

            ItemData displayStack = GetDisplayStack(in inventory, inventorySlot, in itemStack, distributedShare);
            bool hasDisplayItem = displayStack.Count > 0 && displayStack.ID != null;

            slotStates[inventorySlot] = new ItemSlotState(
                Icon: hasDisplayItem ? _itemIcons.Get(displayStack.ID!) : null!,
                Number: inventorySlot < SLOTS_PER_ROW ? _slotNumbers[slotIndex] : string.Empty,
                Count: hasDisplayItem ? displayStack.Count.ToString() : null,
                Equipped: isEquippedHotbar,
                Hovered: isHovered,
                Dragging: isDraggingSource || isDistributedSlot
            );

            ApplyDragGestures(in context, inventorySlot, hovering);

            if (itemStack.Count > 0 && itemStack.ID != null)
            {
                ApplyClickInteraction(in context, inventorySlot, clicked, held, rightClicked, in itemStack);
            }
        }
    }

    private void RefreshDistributedSlotStates(in InventoryComponent inventory, int activeSlot, ItemSlotState[] slotStates)
    {
        for (var i = 0; i < _distributedSlotCount; i++)
        {
            RefreshSlotState(in inventory, activeSlot, _distributedSlots[i], slotStates);
        }

        RefreshSlotState(in inventory, activeSlot, _draggingSlot, slotStates);
    }

    private void RefreshSlotState(in InventoryComponent inventory, int activeSlot, int inventorySlot, ItemSlotState[] slotStates)
    {
        if (inventorySlot < 0 || inventorySlot >= slotStates.Length)
        {
            return;
        }

        ItemData itemStack = GetSlotStack(in inventory, inventorySlot);
        int distributedShare = GetDistributedShare(inventorySlot);
        ItemData displayStack = GetDisplayStack(in inventory, inventorySlot, in itemStack, distributedShare);
        bool hasDisplayItem = displayStack.Count > 0 && displayStack.ID != null;
        bool isDistributedSlot = _rightDistributing && distributedShare > 0;
        bool isDraggingSource = _dragging && inventorySlot == _draggingSlot;
        bool isHovered = inventorySlot == _selectedSlot;
        bool isEquippedHotbar = inventorySlot < SLOTS_PER_ROW && inventorySlot == activeSlot;

        slotStates[inventorySlot] = new ItemSlotState(
            Icon: hasDisplayItem ? _itemIcons.Get(displayStack.ID!) : null!,
            Number: inventorySlot < SLOTS_PER_ROW ? _slotNumbers[inventorySlot] : string.Empty,
            Count: hasDisplayItem ? displayStack.Count.ToString() : null,
            Equipped: isEquippedHotbar,
            Hovered: isHovered,
            Dragging: isDraggingSource || isDistributedSlot
        );
    }

    private ItemData GetDisplayStack(
        in InventoryComponent inventory,
        int inventorySlot,
        in ItemData itemStack,
        int distributedShare
    )
    {
        if (!_rightDistributing)
        {
            return itemStack;
        }

        ItemData dragItemStack = GetSlotStack(in inventory, _draggingSlot);
        if (dragItemStack.IsEmpty())
        {
            return itemStack;
        }

        if (inventorySlot == _draggingSlot)
        {
            int count = distributedShare > 0 ? distributedShare : _distributedRemainder;
            return count > 0 ? dragItemStack with { Count = count } : default;
        }

        if (distributedShare <= 0)
        {
            return itemStack;
        }

        int baseCount = itemStack.IsEmpty() ? 0 : itemStack.Count;
        return dragItemStack with { Count = baseCount + distributedShare };
    }

    /// <summary>Applies the scroll gesture that changes a dragged stack.</summary>
    private void ApplyDragGestures(in SlotRowContext context, int inventorySlot, bool hovering)
    {
        if (!_dragging || _rightDistributing)
        {
            return;
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
        if (context.HandledDragClick)
        {
            return;
        }

        if (context.ShiftHeld)
        {
            //  Shift + click quick moves items via the server-resolved AutoStack op.
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
            return;
        }

        //  Click to pick up this slot onto the cursor
        if (!_dragging && !context.HandledDragClick && clicked)
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
            ResetDragState();
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
        ResetDragState();
        _interactionBlocker?.Dispose();
        _interactionBlocker = null;
        _open = false;
    }
}
