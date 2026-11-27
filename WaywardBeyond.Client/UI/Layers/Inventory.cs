using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Krill;
using Reef;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Items;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Shortcuts;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI.Components;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Localization;
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
    /// The panel height that is not slot rows: the title block, divider spacers, hotbar row,
    /// and panel padding.
    /// </summary>
    private const int PANEL_HEIGHT_CHROME = 137;
    private const int TRAITS_PER_ROW = 2;

    /// <summary>The inputs that every slot of one row shares.</summary>
    private readonly record struct SlotRowContext(
        int RowIndex,
        int ActiveSlot,
        bool MovePressed,
        bool SplitPressed,
        bool SplitDown,
        bool QuickMovePressed,
        bool QuickMoveDown,
        bool HandledDragClick,
        float Scroll,
        InventoryComponent Inventory
    );

    private readonly IShortcutService _shortcutService;
    private readonly IAssetDatabase<Item> _itemDatabase;
    private readonly IBrickDatabase _brickDatabase;
    private readonly LocalizedTags? _invariantTags;
    private readonly ItemIconDatabase _itemIcons;
    private readonly PlayerData _playerData;
    private readonly IECSContext _ecsContext;
    private readonly InteractionState _interactionState;
    private readonly IInputService _inputService;
    private readonly ILocalization _localization;
    private readonly DebugSettings _debugSettings;
    private readonly InventoryView _view;
    private readonly string[] _slotLabels = new string[SLOTS_PER_ROW];
    private int _slotLabelsDirty = 1;

    private bool _open;
    private int _selectedSlot = -1;
    private InteractionState.InteractionBlocker? _interactionBlocker;
    private int _moveStackPressed;
    private int _splitStackPressed;
    private int _quickMovePressed;
    private int _lastQuickMoveSlot = -1;

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
        in ILocalizedTagsDatabase tagDatabase,
        in ItemIconDatabase itemIcons,
        in PlayerData playerData,
        in IECSContext ecsContext,
        in InteractionState interactionState,
        in IInputService inputService,
        in ILocalization localization,
        in DebugSettings debugSettings
    )
    {
        _shortcutService = shortcutService;
        _itemDatabase = itemDatabase;
        _brickDatabase = brickDatabase;
        _invariantTags = tagDatabase.Invariant;
        _itemIcons = itemIcons;
        _playerData = playerData;
        _ecsContext = ecsContext;
        _interactionState = interactionState;
        _inputService = inputService;
        _localization = localization;
        _debugSettings = debugSettings;
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

        GameShortcuts.RegisterOrAddHandler(
            shortcutService,
            GameShortcuts.CreateBack(),
            IsVisible,
            OnCloseInventoryPressed
        );

        for (var i = 0; i < SLOTS_PER_ROW; i++)
        {
            int slotIndex = i;
            GameShortcuts.RegisterOrAddHandler(
                shortcutService,
                GameShortcuts.CreateSlot(slotIndex),
                IsVisible,
                SwapSlots
            );

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

        GameShortcuts.RegisterOrAddHandler(
            shortcutService,
            GameShortcuts.CreateInventoryMoveStack(),
            IsVisible,
            OnMoveStackPressed
        );
        GameShortcuts.RegisterOrAddHandler(
            shortcutService,
            GameShortcuts.CreateInventorySplitStack(),
            IsVisible,
            OnSplitStackPressed
        );
        GameShortcuts.RegisterOrAddHandler(
            shortcutService,
            GameShortcuts.CreateInventoryQuickMove(),
            IsVisible,
            OnQuickMovePressed
        );

        _shortcutService.BindingsChanged += OnBindingsChanged;
        GameShortcuts.ResolveSlotLabels(_shortcutService, _slotLabels);
        _slotLabelsDirty = 0;
    }

    /// <inheritdoc/>
    public bool IsVisible() => _open && WaywardBeyond.IsPlaying();

    /// <inheritdoc/>
    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        if (Interlocked.Exchange(ref _slotLabelsDirty, 0) == 1)
        {
            GameShortcuts.ResolveSlotLabels(_shortcutService, _slotLabels);
        }

        Result<InventoryComponent> inventoryResult = _playerData.GetInventory(_ecsContext.World.DataStore);
        Result<int> activeSlotResult = _playerData.GetActiveSlot(_ecsContext.World.DataStore);
        if (!inventoryResult.Success || !activeSlotResult.Success)
        {
            return Result.FromSuccess();
        }

        InventoryComponent inventory = inventoryResult.Value;
        int activeSlot = activeSlotResult.Value;
        int rowCount = inventory.Contents.Length / SLOTS_PER_ROW;

        bool movePressed = Interlocked.Exchange(ref _moveStackPressed, 0) == 1;
        bool splitPressed = Interlocked.Exchange(ref _splitStackPressed, 0) == 1;
        bool splitDown = splitPressed || _shortcutService.IsHeld(GameShortcuts.INVENTORY_SPLIT_STACK);
        bool quickMovePressed = Interlocked.Exchange(ref _quickMovePressed, 0) == 1;
        bool quickMoveDown = quickMovePressed || _shortcutService.IsHeld(GameShortcuts.INVENTORY_QUICK_MOVE);
        if (!quickMoveDown)
        {
            _lastQuickMoveSlot = -1;
        }

        float scroll = _inputService.GetMouseScroll();

        ItemData dragItemStack = GetSlotStack(in inventory, _draggingSlot);
        if (_dragging && dragItemStack.Count <= 0)
        {
            ResetDragState();
        }

        if (_dragging && splitPressed && !_rightDistributing)
        {
            _rightDistributing = true;
            _rightOriginSlot = _selectedSlot;
            _rightLeftOrigin = false;
            _distributedSlotCount = 0;
        }

        if (_rightDistributing && !splitDown)
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
        bool handledDragClick = ResolveDragClick(movePressed, in dragItemStack, in selectedItemStack);

        if (_rightDistributing)
        {
            RecomputeDistribution(in inventory);
        }

        var slotStates = new ItemSlotState[rowCount * SLOTS_PER_ROW];
        var slotRow = new SlotRowContext(
            RowIndex: 0,
            ActiveSlot: activeSlot,
            MovePressed: movePressed,
            SplitPressed: splitPressed,
            SplitDown: splitDown,
            QuickMovePressed: quickMovePressed,
            QuickMoveDown: quickMoveDown,
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

        IntVector2 cursorPosition = ui.GetCursorPosition();
        IReadOnlyList<InventorySlotViewState> hotbar = rowCount > 0 ? CreateSlotRow(0, slotStates).Slots : [];
        var state = new InventoryViewState(
            CargoRows: cargoRows,
            Hotbar: hotbar,
            Tooltip: CreateTooltip(in inventory),
            HasDrag: _dragging && draggedSlotIcon != null && draggedSlotText != null,
            DragIcon: draggedSlotIcon!,
            DragCount: draggedSlotText,
            DragX: cursorPosition.X,
            DragY: cursorPosition.Y,
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

    /// <summary>Resolves the hovered slot's tooltip content and target anchor.</summary>
    private ItemTooltipState CreateTooltip(in InventoryComponent inventory)
    {
        if (_dragging || _rightDistributing || _selectedSlot < 0)
        {
            return ItemTooltipState.Hidden(_localization);
        }

        ItemData itemStack = GetSlotStack(in inventory, _selectedSlot);
        if (itemStack.Count <= 0 || itemStack.ID == null)
        {
            return ItemTooltipState.Hidden(_localization);
        }

        string debugID = _debugSettings.OverlayVisible ? itemStack.ID : string.Empty;
        string anchorID = $"inventorySlot{_selectedSlot}";
        Result<Item> itemResult = _itemDatabase.Get(itemStack.ID);
        if (!itemResult.Success)
        {
            return new ItemTooltipState(
                Visible: true,
                AnchorID: anchorID,
                NameKey: itemStack.ID,
                Kind: ItemTooltipKind.Unknown,
                DescriptionKey: null,
                TraitRows: [],
                Localization: _localization,
                DebugID: debugID
            );
        }

        Item item = itemResult.Value;
        ItemTooltipKind kind = ResolveItemKind(item);
        IReadOnlyList<ItemTooltipTraitRowState> traitRows = ResolveTraitRows(item);

        return new ItemTooltipState(
            Visible: true,
            AnchorID: anchorID,
            NameKey: item.Name,
            Kind: kind,
            DescriptionKey: item.Description,
            TraitRows: traitRows,
            Localization: _localization,
            DebugID: debugID
        );
    }

    private static ItemTooltipKind ResolveItemKind(Item item)
    {
        if (item.Placeable != null)
        {
            return item.Placeable.Value.Type switch
            {
                PlaceableType.Brick => ItemTooltipKind.Brick,
                PlaceableType.Blueprint => ItemTooltipKind.Blueprint,
                _ => ItemTooltipKind.Item,
            };
        }

        if (item.Tool != null)
        {
            return item.Tool.Value.Type switch
            {
                ToolType.Mining => ItemTooltipKind.MiningTool,
                ToolType.Convert => ItemTooltipKind.ConversionTool,
                ToolType.Sprayer => ItemTooltipKind.PaintTool,
                ToolType.Drill => ItemTooltipKind.TextureTool,
                ToolType.Blueprint => ItemTooltipKind.BlueprintTool,
                _ => ItemTooltipKind.Item,
            };
        }

        return ItemTooltipKind.Item;
    }

    private IReadOnlyList<ItemTooltipTraitRowState> ResolveTraitRows(Item item)
    {
        if (!ItemPlaceables.IsBrickPlaceable(item))
        {
            return [];
        }

        Result<Brick> brickResult = _brickDatabase.Get(item.Placeable!.Value.ID);
        if (!brickResult.Success)
        {
            return [];
        }

        Brick brick = brickResult.Value;
        var traits = new List<ItemTooltipTrait>(4);

        if (brick.Shapeable || brick.IsOrientable(BrickShape.Block))
        {
            traits.Add(ItemTooltipTrait.Orientable);
        }

        if (brick.Shapeable)
        {
            traits.Add(ItemTooltipTrait.Shapeable);
        }

        if (brick.LightSource)
        {
            traits.Add(ItemTooltipTrait.LightSource);
        }

        if (_invariantTags?.Contains(BrickTags.THRUSTER, brick.ID) == true)
        {
            traits.Add(ItemTooltipTrait.Propulsion);
        }

        if (traits.Count == 0)
        {
            return [];
        }

        int rowCount = (traits.Count + TRAITS_PER_ROW - 1) / TRAITS_PER_ROW;
        var rows = new ItemTooltipTraitRowState[rowCount];
        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            int start = rowIndex * TRAITS_PER_ROW;
            int length = Math.Min(TRAITS_PER_ROW, traits.Count - start);
            var rowTraits = new ItemTooltipTrait[length];
            for (var i = 0; i < length; i++)
            {
                rowTraits[i] = traits[start + i];
            }

            rows[rowIndex] = new ItemTooltipTraitRowState(rowTraits);
        }

        return rows;
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
        _lastQuickMoveSlot = -1;
    }

    private void ClearPendingInput()
    {
        Interlocked.Exchange(ref _moveStackPressed, 0);
        Interlocked.Exchange(ref _splitStackPressed, 0);
        Interlocked.Exchange(ref _quickMovePressed, 0);
        _lastQuickMoveSlot = -1;
    }

    private void OnMoveStackPressed()
    {
        Interlocked.Exchange(ref _moveStackPressed, 1);
    }

    private void OnSplitStackPressed()
    {
        Interlocked.Exchange(ref _splitStackPressed, 1);
    }

    private void OnQuickMovePressed()
    {
        Interlocked.Exchange(ref _quickMovePressed, 1);
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

            if (!slotIsSelected && hovering)
            {
                _selectedSlot = inventorySlot;
                slotIsSelected = true;
            }

            if (_rightDistributing && hovering && context.SplitDown)
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
                Number: inventorySlot < SLOTS_PER_ROW ? _slotLabels[slotIndex] : string.Empty,
                Count: hasDisplayItem ? displayStack.Count.ToString() : null,
                Equipped: isEquippedHotbar,
                Hovered: isHovered,
                Dragging: isDraggingSource || isDistributedSlot
            );

            ApplyDragGestures(in context, inventorySlot, hovering);

            if (itemStack.Count > 0 && itemStack.ID != null)
            {
                ApplyClickInteraction(in context, inventorySlot, hovering, in itemStack);
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
            Number: inventorySlot < SLOTS_PER_ROW ? _slotLabels[inventorySlot] : string.Empty,
            Count: hasDisplayItem ? displayStack.Count.ToString() : null,
            Equipped: isEquippedHotbar,
            Hovered: isHovered,
            Dragging: isDraggingSource || isDistributedSlot
        );
    }

    private void OnBindingsChanged()
    {
        Interlocked.Exchange(ref _slotLabelsDirty, 1);
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

    /// <summary>Applies the slot gestures that quick-move, split, or pick up a stack.</summary>
    private void ApplyClickInteraction(
        in SlotRowContext context,
        int inventorySlot,
        bool hovering,
        in ItemData itemStack
    )
    {
        if (context.HandledDragClick || !hovering || _dragging)
        {
            return;
        }

        if (context.QuickMoveDown)
        {
            if (!context.QuickMovePressed && inventorySlot == _lastQuickMoveSlot)
            {
                return;
            }

            _lastQuickMoveSlot = inventorySlot;
            ApplyMove(new SlotMoveOp
            {
                Mode = SlotMoveOpMode.AutoStack,
                FromSlot = inventorySlot,
                ToSlot = -1,
                Count = null,
            });
            return;
        }

        if (context.SplitPressed)
        {
            ApplySplitMove(in context, inventorySlot, in itemStack);
            return;
        }

        if (context.MovePressed)
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
            ClearPendingInput();
            _interactionBlocker?.Dispose();
            _interactionBlocker = null;
            _open = false;
            return;
        }

        if (!_interactionState.TryBlockInteractionExclusive(out _interactionBlocker))
        {
            return;
        }

        ClearPendingInput();
        _open = true;
    }

    private void OnCloseInventoryPressed()
    {
        ResetDragState();
        ClearPendingInput();
        _interactionBlocker?.Dispose();
        _interactionBlocker = null;
        _open = false;
    }
}
