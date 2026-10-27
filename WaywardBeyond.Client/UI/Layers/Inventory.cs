using System;
using System.Numerics;
using Reef;
using Reef.Constraints;
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
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Items;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Renders the modal inventory window, hotbar dock, and hovered item inspection footer.</summary>
internal sealed class Inventory : IUILayer
{
    private const int SLOTS_PER_ROW = 9;
    private const int SLOT_SIZE = 48;
    private const int SLOT_GAP = 6;
    private const int PANEL_PADDING_X = 16;
    private const int HEADER_HEIGHT = 44;
    private const int FOOTER_HEIGHT = 36;
    private const int BADGE_WIDTH = 112;
    private const int BADGE_HEIGHT = 24;

    private static readonly string[] _slotNumbers = ["1", "2", "3", "4", "5", "6", "7", "8", "9"];

    /// <summary>The geometry of the modal inventory frame, in panel space.</summary>
    private readonly record struct PanelLayout(
        int Width,
        int Height,
        int GridWidth,
        int RowCount
    );

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

    private readonly string _titleLabel;
    private readonly string _emptySlotLabel;
    private readonly string _shapeableLabel;
    private readonly string _orientableLabel;

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

        _titleLabel = localization.GetString("ui.inventory.title") ?? "Inventory";
        _emptySlotLabel = localization.GetString("ui.inventory.emptySlot") ?? "Empty slot";
        _shapeableLabel = localization.GetString("ui.inventory.shapeable") ?? "Shapeable";
        _orientableLabel = localization.GetString("ui.inventory.orientable") ?? "Rotatable";

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

        Material? draggedSlotIcon = null;
        string? draggedSlotText = null;
        var slotIsSelected = false;

        ItemData dragItemStack = GetSlotStack(in inventory, _draggingSlot);
        ItemData selectedItemStack = GetSlotStack(in inventory, _selectedSlot);
        ResolveDragRelease(leftHeld, in dragItemStack, in selectedItemStack);

        PanelLayout layout = CreatePanelLayout(rowCount);
        var slotRow = new SlotRowContext(
            RowIndex: 0,
            ActiveSlot: activeSlot,
            LeftHeld: leftHeld,
            RightPressed: rightPressed,
            ShiftHeld: shiftHeld,
            Scroll: scroll,
            Inventory: inventory
        );

        DrawBackdrop(ui);
        DrawPanel(ui, in layout, slotRow, ref slotIsSelected, ref draggedSlotIcon, ref draggedSlotText);

        if (!slotIsSelected)
        {
            _selectedSlot = -1;
        }

        DrawDragPreview(ui, draggedSlotIcon, draggedSlotText);

        return Result.FromSuccess();
    }

    /// <summary>Computes the modal frame geometry from the cargo row count.</summary>
    private static PanelLayout CreatePanelLayout(int rowCount)
    {
        int cargoRowCount = Math.Max(0, rowCount - 1);
        int gridWidth = SLOTS_PER_ROW * SLOT_SIZE + (SLOTS_PER_ROW - 1) * SLOT_GAP;

        int cargoHeight = 0;
        if (cargoRowCount > 0)
        {
            cargoHeight = cargoRowCount * SLOT_SIZE + (cargoRowCount - 1) * SLOT_GAP;
        }

        int dividerY = HEADER_HEIGHT + cargoHeight + 10;
        int hotbarY = dividerY + 11;
        int footerY = hotbarY + SLOT_SIZE + 12;
        int panelWidth = gridWidth + PANEL_PADDING_X * 2;
        int panelHeight = footerY + FOOTER_HEIGHT + 24;

        return new PanelLayout(
            Width: panelWidth,
            Height: panelHeight,
            GridWidth: gridWidth,
            RowCount: rowCount
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

    /// <summary>Draws the full-screen backdrop scrim behind the modal frame.</summary>
    private static void DrawBackdrop(UIBuilder<Material> ui)
    {
        using (ui.Element())
        {
            ui.Passthrough = true;
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = UiTheme.ScrimSoft;
            ui.Constraints = new Constraints
            {
                Width = new Relative(1f),
                Height = new Relative(1f),
            };
        }
    }

    /// <summary>Draws the centered modal frame with its slot rows, divider, and inspection footer.</summary>
    private void DrawPanel(
        UIBuilder<Material> ui,
        in PanelLayout layout,
        SlotRowContext slotRow,
        ref bool slotIsSelected,
        ref Material? draggedSlotIcon,
        ref string? draggedSlotText
    )
    {
        using (ui.MenuPanel(
            new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(layout.Width),
                Height = new Fixed(layout.Height),
            },
            _titleLabel,
            padding: PANEL_PADDING_X
        ))
        {
            DrawCargoRows(ui, in layout, slotRow, ref slotIsSelected, ref draggedSlotIcon, ref draggedSlotText);

            ui.Spacer(height: 10);
            ui.DottedDivider(Widgets.Rect(0, 0, layout.GridWidth, 1));
            ui.Spacer(height: 11);

            DrawHotbarRow(ui, in layout, slotRow, ref slotIsSelected, ref draggedSlotIcon, ref draggedSlotText);
            ui.Spacer(height: 12);
            DrawFooter(ui, slotRow);
        }
    }

    /// <summary>Draws the general cargo rows above the divider.</summary>
    private void DrawCargoRows(
        UIBuilder<Material> ui,
        in PanelLayout layout,
        SlotRowContext slotRow,
        ref bool slotIsSelected,
        ref Material? draggedSlotIcon,
        ref string? draggedSlotText
    )
    {
        for (var rowIndex = 1; rowIndex < layout.RowCount; rowIndex++)
        {
            if (rowIndex > 1)
            {
                ui.Spacer(height: SLOT_GAP);
            }

            SlotRowContext rowContext = slotRow with { RowIndex = rowIndex };
            RenderSlotRow(ui, in rowContext, ref slotIsSelected, ref draggedSlotIcon, ref draggedSlotText);
        }
    }

    /// <summary>Draws the active hotbar row at the bottom so it mirrors the HUD position.</summary>
    private void DrawHotbarRow(
        UIBuilder<Material> ui,
        in PanelLayout layout,
        SlotRowContext slotRow,
        ref bool slotIsSelected,
        ref Material? draggedSlotIcon,
        ref string? draggedSlotText
    )
    {
        if (layout.RowCount <= 0)
        {
            return;
        }

        SlotRowContext rowContext = slotRow with { RowIndex = 0 };
        RenderSlotRow(ui, in rowContext, ref slotIsSelected, ref draggedSlotIcon, ref draggedSlotText);
    }

    /// <summary>Draws the inspection footer for the hovered slot.</summary>
    private void DrawFooter(UIBuilder<Material> ui, SlotRowContext slotRow)
    {
        InventoryComponent inventory = slotRow.Inventory;
        ItemData hoveredStack = GetSlotStack(in inventory, _selectedSlot);
        RenderInspectionFooter(
            ui,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(FOOTER_HEIGHT),
            },
            in hoveredStack
        );
    }

    /// <summary>Draws the dragged stack preview under the cursor.</summary>
    private void DrawDragPreview(UIBuilder<Material> ui, Material? draggedSlotIcon, string? draggedSlotText)
    {
        if (!_dragging || draggedSlotText == null || draggedSlotIcon == null)
        {
            return;
        }

        Vector2 cursorPosition = _inputService.CursorPosition;

        using (ui.Element())
        {
            ui.Passthrough = true;
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Local,
                X = new Fixed((int)cursorPosition.X),
                Y = new Fixed((int)cursorPosition.Y),
                Width = new Fixed(SLOT_SIZE),
                Height = new Fixed(SLOT_SIZE),
            };

            using (ui.Image(draggedSlotIcon))
            {
                ui.Passthrough = true;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                    Width = new Fill(),
                    Height = new Fill(),
                };
            }

            using (ui.Text(draggedSlotText, UiMetrics.TINY_FONT))
            {
                ui.Passthrough = true;
                ui.Color = UiTheme.Text;
                ui.BackgroundColor = UiTheme.ScrimSoft;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Bottom | Anchors.Right,
                };
            }
        }
    }

    /// <summary>Draws one row of slots and processes the interaction of each slot.</summary>
    private void RenderSlotRow(
        UIBuilder<Material> ui,
        in SlotRowContext context,
        ref bool slotIsSelected,
        ref Material? draggedSlotIcon,
        ref string? draggedSlotText
    )
    {
        InventoryComponent inventory = context.Inventory;

        using (ui.Row(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(SLOT_SIZE),
            },
            spacing: SLOT_GAP
        ))
        {
            for (var slotIndex = 0; slotIndex < SLOTS_PER_ROW; slotIndex++)
            {
                int inventorySlot = context.RowIndex * SLOTS_PER_ROW + slotIndex;
                ItemData itemStack = GetSlotStack(in inventory, inventorySlot);

                using (ui.Element())
                {
                    ui.LayoutDirection = LayoutDirection.None;
                    ui.Padding = new Padding(left: 4, top: 4, right: 4, bottom: 4);
                    ui.Constraints = new Constraints
                    {
                        Width = new Fixed(SLOT_SIZE),
                        Height = new Fixed(SLOT_SIZE),
                    };

                    bool hovering = ui.Hovering($"inventorySlot{inventorySlot}");
                    bool targeting = hovering || (_dragging && IsCursorOverSlot(ui, inventorySlot));
                    bool clicked = ui.Clicked($"inventorySlot{inventorySlot}");
                    bool rightClicked = targeting && context.RightPressed;
                    bool held = hovering && context.LeftHeld;

                    if (!slotIsSelected && targeting)
                    {
                        _selectedSlot = inventorySlot;
                        slotIsSelected = true;
                    }

                    bool isDraggingSource = _dragging && inventorySlot == _draggingSlot;
                    bool isHovered = inventorySlot == _selectedSlot;
                    bool isEquippedHotbar = inventorySlot < SLOTS_PER_ROW && inventorySlot == context.ActiveSlot;

                    ApplySlotChrome(ui, isDraggingSource, isHovered, isEquippedHotbar);
                    Material? icon = DrawSlotIcon(ui, in itemStack);

                    if (inventorySlot < SLOTS_PER_ROW)
                    {
                        DrawSlotNumber(ui, slotIndex, isEquippedHotbar, isHovered);
                    }

                    ApplyDragGestures(in context, inventorySlot, targeting, rightClicked);

                    if (itemStack.Count == 0 || itemStack.ID == null)
                    {
                        continue;
                    }

                    string text = DrawCountBadge(ui, in itemStack);

                    if (_dragging && _draggingSlot == inventorySlot)
                    {
                        draggedSlotIcon = icon;
                        draggedSlotText = text;
                    }

                    ApplyClickInteraction(in context, inventorySlot, clicked, held, rightClicked, in itemStack);
                }
            }
        }
    }

    /// <summary>Checks the cursor against a slot even when the drag source owns hover input.</summary>
    private static bool IsCursorOverSlot(UIBuilder<Material> ui, int inventorySlot)
    {
        IntVector2 cursor = ui.GetRelativeCursorPosition($"inventorySlot{inventorySlot}");
        return cursor.X >= 0
            && cursor.Y >= 0
            && cursor.X < SLOT_SIZE
            && cursor.Y < SLOT_SIZE;
    }

    /// <summary>Draws the item icon of a slot and returns it for the drag preview.</summary>
    private Material? DrawSlotIcon(UIBuilder<Material> ui, in ItemData itemStack)
    {
        if (itemStack.Count <= 0 || itemStack.ID == null)
        {
            return null;
        }

        Material slotIcon = _itemIcons.Get(itemStack.ID);
        using (ui.Image(slotIcon))
        {
            ui.Passthrough = true;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fill(),
                Height = new Fill(),
            };
        }

        return slotIcon;
    }

    /// <summary>Draws the hotbar slot number tinted by the slot state.</summary>
    private static void DrawSlotNumber(UIBuilder<Material> ui, int slotIndex, bool isEquippedHotbar, bool isHovered)
    {
        using (ui.Text(_slotNumbers[slotIndex], UiMetrics.TINY_FONT))
        {
            ui.Passthrough = true;
            if (isEquippedHotbar)
            {
                ui.Color = UiTheme.PrimaryBright;
            }
            else if (isHovered)
            {
                ui.Color = UiTheme.AccentBright;
            }
            else
            {
                ui.Color = UiTheme.TextDim;
            }
        }
    }

    /// <summary>Draws the stack count badge of a slot and returns its text.</summary>
    private static string DrawCountBadge(UIBuilder<Material> ui, in ItemData itemStack)
    {
        string text = itemStack.Count.ToString();
        using (ui.Text(text, UiMetrics.TINY_FONT))
        {
            ui.Passthrough = true;
            ui.Color = UiTheme.Text;
            ui.BackgroundColor = UiTheme.ScrimSoft;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Bottom | Anchors.Right,
            };
        }

        return text;
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

    private static void ApplySlotChrome(UIBuilder<Material> ui, bool isDraggingSource, bool isHovered, bool isEquippedHotbar)
    {
        if (isDraggingSource)
        {
            ui.Color = UiTheme.AccentDark;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
                Color = UiTheme.Accent,
            };
            ui.CornerCaps = default;
            return;
        }

        if (isHovered)
        {
            ui.Color = UiTheme.ButtonHover;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
                Color = UiTheme.Accent,
            };
            ui.CornerCaps = new CornerCapStyle
            {
                Length = UiMetrics.SELECTION_CAP_LENGTH,
                Thickness = UiMetrics.SELECTION_CAP_THICKNESS,
                Color = UiTheme.AccentBright,
            };
            return;
        }

        if (isEquippedHotbar)
        {
            ui.Color = UiTheme.SelectionBackground;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
                Color = UiTheme.Primary,
            };
            ui.CornerCaps = new CornerCapStyle
            {
                Length = UiMetrics.SELECTION_CAP_LENGTH,
                Thickness = UiMetrics.SELECTION_CAP_THICKNESS,
                Color = UiTheme.PrimaryBright,
            };
            return;
        }

        ui.Color = UiTheme.ControlBackground;
        ui.Border = new BorderStyle
        {
            Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
            Color = UiTheme.Border,
        };
        ui.CornerCaps = default;
    }

    /// <summary>Draws the hovered item name and its capability badges.</summary>
    private void RenderInspectionFooter(UIBuilder<Material> ui, Constraints constraints, in ItemData itemStack)
    {
        using (ui.Row(constraints, spacing: 8))
        {
            ui.Padding = new Padding(left: 12, top: 8, right: 12, bottom: 8);
            ui.Color = UiTheme.PanelInner;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
                Color = UiTheme.Border,
            };
            ui.ClipConstraints = Widgets.ClipToBounds();

            if (itemStack.Count <= 0 || itemStack.ID == null)
            {
                ui.Label(
                    _emptySlotLabel,
                    UiMetrics.SMALL_FONT,
                    UiTheme.TextDim,
                    new Constraints
                    {
                        Width = new Fill(),
                        Height = new Fill(),
                    }
                );
                return;
            }

            Result<Item> itemResult = _itemDatabase.Get(itemStack.ID);
            if (!itemResult.Success)
            {
                string fallbackName = _localization.GetString(itemStack.ID) ?? itemStack.ID;
                ui.Label(
                    fallbackName,
                    UiMetrics.SMALL_FONT,
                    UiTheme.TextMuted,
                    new Constraints
                    {
                        Width = new Fill(),
                        Height = new Fill(),
                    }
                );
                return;
            }

            Item item = itemResult.Value;
            string itemName = _localization.GetString(item.Name) ?? item.Name;

            using (ui.Element())
            {
                ui.Color = UiTheme.Primary;
                ui.Constraints = new Constraints
                {
                    Width = new Fixed(2),
                    Height = new Fill(),
                };
            }

            ui.Label(
                itemName,
                UiMetrics.SMALL_FONT,
                UiTheme.PrimaryBright,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                }
            );

            if (item.Placeable == null)
            {
                return;
            }

            Result<Brick> brickResult = _brickDatabase.Get(item.Placeable.Value.ID);
            if (!brickResult.Success)
            {
                return;
            }

            Brick brick = brickResult.Value;
            bool shapeable = brick.Shapeable;
            bool orientable = brick.Shapeable || brick.IsOrientable(BrickShape.Block);

            if (orientable)
            {
                RenderCapabilityBadge(ui, MenuGlyphs.ROTATE, _orientableLabel);
            }

            if (shapeable)
            {
                RenderCapabilityBadge(ui, MenuGlyphs.SHAPES, _shapeableLabel);
            }
        }
    }

    private static void RenderCapabilityBadge(UIBuilder<Material> ui, string iconGlyph, string label)
    {
        using (ui.Row(
            new Constraints
            {
                Width = new Fixed(BADGE_WIDTH),
                Height = new Fixed(BADGE_HEIGHT),
                Anchors = Anchors.Center,
            },
            spacing: 2
        ))
        {
            ui.Padding = new Padding(left: 6, top: 0, right: 6, bottom: 0);
            ui.Color = UiTheme.AccentDark;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
                Color = UiTheme.BorderHover,
            };
            ui.ClipConstraints = Widgets.ClipToBounds();

            ui.Label(
                iconGlyph,
                UiMetrics.SMALL_FONT,
                UiTheme.AccentBright,
                new Constraints
                {
                    Width = new Fixed(20),
                    Height = new Fill(),
                },
                HorizontalAlignment.Center,
                fontId: UiTheme.FONT_ICONS
            );
            ui.Label(
                label,
                UiMetrics.TINY_FONT,
                UiTheme.Text,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                }
            );
        }
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
