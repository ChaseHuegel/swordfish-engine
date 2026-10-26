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
    private const int BADGE_GAP = 6;

    private static readonly string[] _slotNumbers = ["1", "2", "3", "4", "5", "6", "7", "8", "9"];

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
        var slotIsSelected = false;
        int rowCount = inventory.Contents.Length / SLOTS_PER_ROW;

        bool leftHeld = ui.LeftHeld();
        bool rightPressed = ui.RightPressed();
        bool shiftHeld = _inputService.IsKeyHeld(Key.Shift);
        float scroll = _inputService.GetMouseScroll();

        Material? draggedSlotIcon = null;
        string? draggedSlotText = null;

        ItemData dragItemStack = GetSlotStack(in inventory, _draggingSlot);
        ItemData selectedItemStack = GetSlotStack(in inventory, _selectedSlot);
        bool isDragSlotEmpty = dragItemStack.Count <= 0;

        if (_dragging && (!leftHeld || isDragSlotEmpty))
        {
            _dragging = false;

            if (!isDragSlotEmpty && _selectedSlot >= 0)
            {
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
                }
                else
                {
                    ApplyMove(new SlotMoveOp
                    {
                        Mode = SlotMoveOpMode.Exact,
                        FromSlot = _draggingSlot,
                        ToSlot = _selectedSlot,
                        Count = null,
                    });
                }
            }
        }

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
        int panelHeight = footerY + FOOTER_HEIGHT + 16;

        //  Full-screen backdrop scrim
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

        //  Centered modal inventory frame
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(panelWidth),
                Height = new Fixed(panelHeight),
            };

            using (ui.MenuPanel(Widgets.Rect(0, 0, panelWidth, panelHeight), _titleLabel))
            {
                //  Render general cargo rows (rows 1..rowCount-1) at the top
                for (var rowIndex = 1; rowIndex < rowCount; rowIndex++)
                {
                    int rowY = HEADER_HEIGHT + (rowIndex - 1) * (SLOT_SIZE + SLOT_GAP);
                    RenderSlotRow(
                        ui,
                        in inventory,
                        rowIndex,
                        rowY,
                        activeSlot,
                        leftHeld,
                        rightPressed,
                        shiftHeld,
                        scroll,
                        ref slotIsSelected,
                        ref draggedSlotIcon,
                        ref draggedSlotText
                    );
                }

                //  Dotted divider separating cargo storage from the equipped hotbar row
                ui.DottedDivider(Widgets.Rect(PANEL_PADDING_X, dividerY, gridWidth, 1));

                //  Render the active hotbar row (row 0) at the bottom so it mirrors the HUD position
                if (rowCount > 0)
                {
                    RenderSlotRow(
                        ui,
                        in inventory,
                        rowIndex: 0,
                        hotbarY,
                        activeSlot,
                        leftHeld,
                        rightPressed,
                        shiftHeld,
                        scroll,
                        ref slotIsSelected,
                        ref draggedSlotIcon,
                        ref draggedSlotText
                    );
                }

                //  Hovered item inspection footer
                ItemData hoveredStack = GetSlotStack(in inventory, _selectedSlot);
                RenderInspectionFooter(ui, Widgets.Rect(PANEL_PADDING_X, footerY, gridWidth, FOOTER_HEIGHT), in hoveredStack);
            }
        }

        if (!slotIsSelected)
        {
            _selectedSlot = -1;
        }

        if (_dragging && draggedSlotText != null && draggedSlotIcon != null)
        {
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

                using (ui.Text(draggedSlotText, UiMetrics.TinyFont))
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

        return Result.FromSuccess();
    }

    private void RenderSlotRow(
        UIBuilder<Material> ui,
        in InventoryComponent inventory,
        int rowIndex,
        int rowY,
        int activeSlot,
        bool leftHeld,
        bool rightPressed,
        bool shiftHeld,
        float scroll,
        ref bool slotIsSelected,
        ref Material? draggedSlotIcon,
        ref string? draggedSlotText
    )
    {
        for (var slotIndex = 0; slotIndex < SLOTS_PER_ROW; slotIndex++)
        {
            int inventorySlot = rowIndex * SLOTS_PER_ROW + slotIndex;
            ItemData itemStack = GetSlotStack(in inventory, inventorySlot);
            int slotX = PANEL_PADDING_X + slotIndex * (SLOT_SIZE + SLOT_GAP);

            using (ui.Element())
            {
                ui.LayoutDirection = LayoutDirection.None;
                ui.Padding = new Padding(left: 4, top: 4, right: 4, bottom: 4);
                ui.Constraints = Widgets.RectConstraints(Widgets.Rect(slotX, rowY, SLOT_SIZE, SLOT_SIZE));

                bool hovering = ui.Hovering($"inventorySlot{inventorySlot}");
                bool clicked = ui.Clicked($"inventorySlot{inventorySlot}");
                bool rightClicked = hovering && rightPressed;
                bool held = hovering && leftHeld;

                if (!slotIsSelected && hovering)
                {
                    _selectedSlot = inventorySlot;
                    slotIsSelected = true;
                }

                bool isDraggingSource = _dragging && inventorySlot == _draggingSlot;
                bool isHovered = inventorySlot == _selectedSlot;
                bool isEquippedHotbar = inventorySlot < SLOTS_PER_ROW && inventorySlot == activeSlot;

                ApplySlotChrome(ui, isDraggingSource, isHovered, isEquippedHotbar);

                Material? icon = null;
                if (itemStack.Count > 0 && itemStack.ID != null)
                {
                    Material slotIcon = _itemIcons.Get(itemStack.ID);
                    icon = slotIcon;
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
                }

                if (inventorySlot < SLOTS_PER_ROW)
                {
                    using (ui.Text(_slotNumbers[slotIndex], UiMetrics.TinyFont))
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

                //  Right-clicking a slot while dragging an item drops 1 count
                if (_dragging && rightClicked)
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
                if (_dragging && hovering && scroll != 0f)
                {
                    int sourceSlot;
                    int destinationSlot;
                    if (scroll > 0f)
                    {
                        sourceSlot = _draggingSlot;
                        destinationSlot = inventorySlot;
                    }
                    else
                    {
                        sourceSlot = inventorySlot;
                        destinationSlot = _draggingSlot;
                    }

                    var amount = (int)Math.Abs(scroll);

                    ApplyMove(new SlotMoveOp
                    {
                        Mode = SlotMoveOpMode.Exact,
                        FromSlot = sourceSlot,
                        ToSlot = destinationSlot,
                        Count = (uint)amount,
                    });
                }

                if (itemStack.Count == 0 || itemStack.ID == null)
                {
                    continue;
                }

                var text = itemStack.Count.ToString();
                using (ui.Text(text, UiMetrics.TinyFont))
                {
                    ui.Passthrough = true;
                    ui.Color = UiTheme.Text;
                    ui.BackgroundColor = UiTheme.ScrimSoft;
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Bottom | Anchors.Right,
                    };
                }

                if (_dragging && _draggingSlot == inventorySlot)
                {
                    draggedSlotIcon = icon;
                    draggedSlotText = text;
                }

                if (shiftHeld)
                {
                    //  Shift + click and shift + hold left click quick moves items via the
                    //  server-resolved AutoStack op.
                    if (clicked || held)
                    {
                        ApplyMove(new SlotMoveOp
                        {
                            Mode = SlotMoveOpMode.AutoStack,
                            FromSlot = inventorySlot,
                            ToSlot = -1,
                            Count = null,
                        });
                    }
                }
                else
                {
                    //  Right-clicking a slot splits the stack: move half to the first
                    //  empty slot (client-computed target, server revalidated).
                    if (!_dragging && rightClicked)
                    {
                        int splitTarget = InventoryResolver.FindFirstEmptySlot(in inventory);
                        if (splitTarget >= 0)
                        {
                            ApplyMove(new SlotMoveOp
                            {
                                Mode = SlotMoveOpMode.Exact,
                                FromSlot = inventorySlot,
                                ToSlot = splitTarget,
                                Count = (uint)Math.Max(1, itemStack.Count / 2),
                            });
                        }
                    }

                    //  Start dragging this slot
                    if (!_dragging && held)
                    {
                        _dragging = true;
                        _draggingSlot = inventorySlot;
                    }
                }
            }
        }
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
                Thickness = UiMetrics.PanelBorderThickness,
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
                Thickness = UiMetrics.PanelBorderThickness,
                Color = UiTheme.Accent,
            };
            ui.CornerCaps = new CornerCapStyle
            {
                Length = UiMetrics.SelectionCapLength,
                Thickness = UiMetrics.SelectionCapThickness,
                Color = UiTheme.AccentBright,
            };
            return;
        }

        if (isEquippedHotbar)
        {
            ui.Color = UiTheme.SelectionBackground;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PanelBorderThickness,
                Color = UiTheme.Primary,
            };
            ui.CornerCaps = new CornerCapStyle
            {
                Length = UiMetrics.SelectionCapLength,
                Thickness = UiMetrics.SelectionCapThickness,
                Color = UiTheme.PrimaryBright,
            };
            return;
        }

        ui.Color = UiTheme.ControlBackground;
        ui.Border = new BorderStyle
        {
            Thickness = UiMetrics.PanelBorderThickness,
            Color = UiTheme.Border,
        };
        ui.CornerCaps = default;
    }

    private void RenderInspectionFooter(UIBuilder<Material> ui, IntRect rect, in ItemData itemStack)
    {
        using (ui.Element())
        {
            ui.Passthrough = true;
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = UiTheme.PanelInner;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PanelBorderThickness,
                Color = UiTheme.Border,
            };
            ui.Constraints = Widgets.RectConstraints(rect);
            ui.ClipConstraints = Widgets.RectConstraints(rect);

            if (itemStack.Count <= 0 || itemStack.ID == null)
            {
                ui.Label(
                    _emptySlotLabel,
                    Widgets.Rect(12, 0, rect.Size.X - 24, rect.Size.Y),
                    UiMetrics.SmallFont,
                    UiTheme.TextDim
                );
                return;
            }

            Result<Item> itemResult = _itemDatabase.Get(itemStack.ID);
            if (!itemResult.Success)
            {
                string fallbackName = _localization.GetString(itemStack.ID) ?? itemStack.ID;
                ui.Label(
                    fallbackName,
                    Widgets.Rect(12, 0, rect.Size.X - 24, rect.Size.Y),
                    UiMetrics.SmallFont,
                    UiTheme.TextMuted
                );
                return;
            }

            Item item = itemResult.Value;
            string itemName = _localization.GetString(item.Name) ?? item.Name;

            //  Left primary accent bar and localized item name
            ui.Divider(Widgets.Rect(8, 8, 2, rect.Size.Y - 16), UiTheme.Primary);
            ui.Label(
                itemName,
                Widgets.Rect(16, 0, rect.Size.X - 32, rect.Size.Y),
                UiMetrics.SmallFont,
                UiTheme.PrimaryBright
            );

            //  Right-aligned Font Awesome capability badges for placeable bricks
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

            int badgeX = rect.Size.X - 6 - BADGE_WIDTH;
            int badgeY = (rect.Size.Y - BADGE_HEIGHT) / 2;

            if (orientable)
            {
                RenderCapabilityBadge(ui, Widgets.Rect(badgeX, badgeY, BADGE_WIDTH, BADGE_HEIGHT), MenuGlyphs.Rotate, _orientableLabel);
                badgeX -= BADGE_WIDTH + BADGE_GAP;
            }

            if (shapeable)
            {
                RenderCapabilityBadge(ui, Widgets.Rect(badgeX, badgeY, BADGE_WIDTH, BADGE_HEIGHT), MenuGlyphs.Shapes, _shapeableLabel);
            }
        }
    }

    private static void RenderCapabilityBadge(UIBuilder<Material> ui, IntRect rect, string iconGlyph, string label)
    {
        using (ui.Element())
        {
            ui.Passthrough = true;
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = UiTheme.AccentDark;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PanelBorderThickness,
                Color = UiTheme.BorderHover,
            };
            ui.Constraints = Widgets.RectConstraints(rect);
            ui.ClipConstraints = Widgets.RectConstraints(rect);

            ui.Label(
                iconGlyph,
                Widgets.Rect(6, 0, 20, rect.Size.Y),
                UiMetrics.SmallFont,
                UiTheme.AccentBright,
                Anchors.Center,
                UiTheme.FontIcons
            );
            ui.Label(
                label,
                Widgets.Rect(28, 0, rect.Size.X - 32, rect.Size.Y),
                UiMetrics.TinyFont,
                UiTheme.Text
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
