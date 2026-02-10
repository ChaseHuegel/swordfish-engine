using System;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Extensions;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Items;
using WaywardBeyond.Client.Core.Player;
using WaywardBeyond.Client.Core.Systems;

namespace WaywardBeyond.Client.Core.UI.Layers;

internal class Inventory : IUILayer
{
    private const int SLOTS_PER_ROW = 9;

    private readonly IAssetDatabase<Item> _itemDatabase;
    private readonly PlayerData _playerData;
    private readonly IECSContext _ecsContext;
    private readonly PlayerInteractionService _playerInteractionService;
    private readonly IInputService _inputService;

    private readonly Vector4 _backgroundColor;
    private readonly Vector4 _slotColor;
    private readonly Vector4 _selectedColor;
    private readonly Vector4 _dragColor;

    private bool _open;
    private int _selectedSlot = -1;
    private PlayerInteractionService.InteractionBlocker? _interactionBlocker;
    
    private bool _dragging;
    private int _draggingSlot;
    
    public Inventory(
        in IShortcutService shortcutService,
        in IAssetDatabase<Item> itemDatabase,
        in PlayerData playerData,
        in IECSContext ecsContext,
        in PlayerInteractionService playerInteractionService,
        in IInputService inputService
    ) {
        _itemDatabase = itemDatabase;
        _playerData = playerData;
        _ecsContext = ecsContext;
        _playerInteractionService = playerInteractionService;
        _inputService = inputService;

        _backgroundColor = Color.FromArgb(int.Parse("FF4F546B", NumberStyles.HexNumber)).ToVector4();
        _slotColor = Color.FromArgb(int.Parse("FF3978A8", NumberStyles.HexNumber)).ToVector4();
        _selectedColor = Color.FromArgb(int.Parse("FF8AEBF1", NumberStyles.HexNumber)).ToVector4();
        _dragColor = Color.FromArgb(int.Parse("FF3940A8", NumberStyles.HexNumber)).ToVector4();
        
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
                
                Result<InventoryComponent> inventoryResult = _playerData.GetInventory(_ecsContext.World.DataStore);
                if (!inventoryResult.Success)
                {
                    return;
                }
                
                InventoryComponent inventory = inventoryResult.Value;
                inventory.Swap(slotIndex, _selectedSlot);
            }
        }
    }
    
    public bool IsVisible()
    {
        return _open && WaywardBeyond.IsPlaying();
    }

    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        Result<InventoryComponent> inventoryResult = _playerData.GetInventory(_ecsContext.World.DataStore);
        Result<int> activeSlotResult = _playerData.GetActiveSlot(_ecsContext.World.DataStore);
        if (!inventoryResult || !activeSlotResult)
        {
            return Result.FromSuccess();
        }
        
        InventoryComponent inventory = inventoryResult.Value;
        var slotIsSelected = false;
        int rowCount = inventory.Contents.Length / SLOTS_PER_ROW;
        
        bool leftHeld = ui.LeftHeld();
        bool rightPressed = ui.RightPressed();
        bool shiftHeld = _inputService.IsKeyHeld(Key.Shift);
        float scroll = _inputService.GetMouseScroll();
        
        Material? draggedSlotIcon = null;
        string? draggedSlotText = null;
        
        ItemStack dragItemStack = inventory.Contents[_draggingSlot];
        ItemStack selectedItemStack = _selectedSlot != -1 ? inventory.Contents[_selectedSlot] : ItemStack.Empty;
        bool isDragSlotEmpty = dragItemStack.Count <= 0;
        
        if (_dragging && (!leftHeld || isDragSlotEmpty))
        {
            //  Release dragging
            _dragging = false;
            
            //  Drop dragged items
            if (!isDragSlotEmpty)
            {
                //  Try to fill the selected stack from the dragged stack, if it is the same as the dragged item
                if (dragItemStack.ID == selectedItemStack.ID)
                {
                    int available = selectedItemStack.MaxSize - selectedItemStack.Count;
                    Result<ItemStack> content = inventory.Remove(_draggingSlot, available);
                    if (content.Success)
                    {
                        if (!inventory.Add(_selectedSlot, content))
                        {
                            inventory.Add(content);
                            //  TODO if this fails, the item should be dropped so it isn't lost
                        }
                    }
                } 
                //  Otherwise, swap the slots
                else
                {
                    inventory.Swap(_draggingSlot, _selectedSlot);
                }
            }
        }
        
        //  Inventory
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Color = _backgroundColor;
            ui.Padding = new Padding(
                left: 4,
                top: 4,
                right: 4,
                bottom: 4
            );
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };

            for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                //  Row
                using (ui.Element())
                {
                    ui.Color = Vector4.Zero;

                    for (var slotIndex = 0; slotIndex < SLOTS_PER_ROW; slotIndex++)
                    {
                        int inventorySlot = rowIndex * SLOTS_PER_ROW + slotIndex;
                        ItemStack itemStack = inventory.Contents?.Length > inventorySlot ? inventory.Contents[inventorySlot] : default;

                        //  Slot
                        using (ui.Element())
                        {
                            ui.LayoutDirection = LayoutDirection.None;
                            ui.Color = _backgroundColor;
                            ui.Padding = new Padding(left: 4, top: 4, right: 4, bottom: 4);
                            ui.Constraints = new Constraints
                            {
                                Width = new Fixed(56),
                                Height = new Fixed(56),
                            };
                            
                            //  Check interactions with this slot
                            bool hovering = ui.Hovering($"inventorySlot{inventorySlot}");
                            bool clicked = ui.Clicked($"inventorySlot{inventorySlot}");
                            bool rightClicked = hovering && rightPressed;
                            bool held = hovering && leftHeld;

                            using (ui.Element())
                            {
                                ui.LayoutDirection = LayoutDirection.None;
                                ui.Padding = new Padding(left: 2, top: 2, right: 2, bottom: 2);
                                ui.Constraints = new Constraints
                                {
                                    Width = new Fixed(48),
                                    Height = new Fixed(48),
                                };

                                ui.Color = inventorySlot switch
                                {
                                    _ when inventorySlot == _draggingSlot && _dragging => _dragColor,
                                    _ when inventorySlot == _selectedSlot => _selectedColor,
                                    _ => _slotColor,
                                };

                                if (!slotIsSelected && hovering)
                                {
                                    _selectedSlot = inventorySlot;
                                    slotIsSelected = true;
                                }
                                
                                //  Slot number (only display for the hotbar slots)
                                if (inventorySlot < SLOTS_PER_ROW)
                                {
                                    int slotNumber = slotIndex + 1;
                                    using (ui.Text(slotNumber.ToString()))
                                    {
                                        ui.FontSize = 16;
                                    }
                                }
                                
                                //  Process base slot interactions
                                
                                //  Right-clicking a slot while dragging an item drops 1 count
                                if (_dragging && rightClicked)
                                {
                                    Result<ItemStack> content = inventory.Remove(_draggingSlot, 1);
                                    if (content.Success)
                                    {
                                        if (!inventory.Add(inventorySlot, content) && !inventory.Add(_draggingSlot, content))
                                        {
                                            inventory.Add(content);
                                            //  TODO if this fails, the item should be dropped so it isn't lost
                                        }
                                    }
                                }
                                
                                //  Scrolling while dragging an item will add/remove items by 1
                                if (_dragging && hovering && scroll != 0f)
                                {
                                    int sourceSlot = scroll > 0f ? _draggingSlot : inventorySlot;
                                    int destinationSlot = scroll > 0f ? inventorySlot : _draggingSlot;
                                    var amount = (int)Math.Abs(scroll);
                                    
                                    Result<ItemStack> content = inventory.Remove(sourceSlot, amount);
                                    if (content.Success)
                                    {
                                        if (!inventory.Add(destinationSlot, content) && !inventory.Add(sourceSlot, content))
                                        {
                                            inventory.Add(content);
                                            //  TODO if this fails, the item should be dropped so it isn't lost
                                        }
                                    }
                                }
                                
                                //  Continue if this slot is empty
                                if (itemStack.Count == 0 || itemStack.ID == null)
                                {
                                    continue;
                                }

                                Result<Item> itemResult = _itemDatabase.Get(itemStack.ID);

                                //  Stack size
                                var text = itemStack.Count.ToString();
                                using (ui.Text(text))
                                {
                                    ui.FontSize = 16;
                                    ui.Constraints = new Constraints
                                    {
                                        Anchors = Anchors.Bottom | Anchors.Right,
                                    };
                                }

                                //  Icon
                                Material icon = itemResult.Success ? itemResult.Value.Icon : null!;
                                using (ui.Image(icon))
                                {
                                    ui.Constraints = new Constraints
                                    {
                                        Anchors = Anchors.Center,
                                        Width = new Fill(),
                                        Height = new Fill(),
                                    };
                                }

                                if (_dragging && _draggingSlot == inventorySlot)
                                {
                                    draggedSlotIcon = icon;
                                    draggedSlotText = text;
                                }
                                
                                //  Process populated slot interactions

                                if (shiftHeld)
                                {
                                    //  Shift + click and shift + hold left click quick moves items
                                    if (clicked || held)
                                    {
                                        Result<ItemStack> content = inventory.Remove(inventorySlot);
                                        if (content.Success)
                                        {
                                            int startingSlot = inventorySlot < SLOTS_PER_ROW ? SLOTS_PER_ROW : 0;
                                            inventory.Add(content, startingSlot);
                                        }
                                    }
                                }
                                else
                                {
                                    //  Right-clicking a slot splits the stack
                                    if (!_dragging && rightClicked)
                                    {
                                        Result<ItemStack> content = inventory.Remove(inventorySlot, itemStack.Count / 2);
                                        if (content.Success)
                                        {
                                            if (!inventory.Add(content, onlyEmptySlots: true) && !inventory.Add(inventorySlot, content))
                                            {
                                                inventory.Add(content);
                                                //  TODO if this fails, the item should be dropped so it isn't lost
                                            }
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
                }
            }
        }

        //  If nothing is selected this frame, clear the selection
        if (!slotIsSelected)
        {
            _selectedSlot = -1;
        }
        
        if (_dragging && draggedSlotText != null && draggedSlotIcon != null)
        {
            Vector2 cursorPosition = _inputService.CursorPosition;
            
            //  Slot
            using (ui.Element())
            {
                ui.LayoutDirection = LayoutDirection.None;
                ui.Padding = new Padding(left: 4, top: 4, right: 4, bottom: 4);
                ui.Constraints = new Constraints
                {
                    X = new Fixed((int)cursorPosition.X),
                    Y = new Fixed((int)cursorPosition.Y),
                    Width = new Fixed(48),
                    Height = new Fixed(48),
                    Anchors = Anchors.Left | Anchors.Top,
                };
                
                //  Stack size
                using (ui.Text(draggedSlotText))
                {
                    ui.FontSize = 16;
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Bottom | Anchors.Right,
                        X = new Relative(1f),
                        Y = new Relative(1f),
                    };
                }

                //  Icon
                using (ui.Image(draggedSlotIcon))
                {
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                        X = new Relative(0.5f),
                        Y = new Relative(0.5f),
                        Width = new Fill(),
                        Height = new Fill(),
                    };
                }
            }
        }
        
        return Result.FromSuccess();
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
        
        if (!_playerInteractionService.TryBlockInteractionExclusive(out _interactionBlocker))
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
