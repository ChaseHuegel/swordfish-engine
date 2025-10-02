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

namespace WaywardBeyond.Client.Core.UI;

internal class Hotbar : IUILayer
{
    private const int SLOT_COUNT = 9;

    private readonly IAssetDatabase<Item> _itemDatabase;
    private readonly IInputService _inputService;
    private readonly PlayerData _playerData;
    private readonly IECSContext _ecsContext;

    private readonly Vector4 _backgroundColor;
    private readonly Vector4 _slotColor;
    private readonly Vector4 _selectedColor;
    
    public Hotbar(
        IShortcutService shortcutService,
        IInputService inputService,
        IAssetDatabase<Item> itemDatabase,
        PlayerData playerData,
        IECSContext ecsContext
    ) {
        _inputService = inputService;
        _itemDatabase = itemDatabase;
        _playerData = playerData;
        _ecsContext = ecsContext;
        
        _backgroundColor = Color.FromArgb(int.Parse("FF4F546B", NumberStyles.HexNumber)).ToVector4();
        _slotColor = Color.FromArgb(int.Parse("FF3978A8", NumberStyles.HexNumber)).ToVector4();
        _selectedColor = Color.FromArgb(int.Parse("FF8AEBF1", NumberStyles.HexNumber)).ToVector4();
        
        for (var i = 0; i < SLOT_COUNT; i++)
        {
            int slotNumber = i + 1;
            int slotIndex = i;
            var shortcut = new Shortcut
            {
                Name = $"Slot {slotNumber}",
                Category = "Interaction",
                Modifiers = ShortcutModifiers.None,
                Key = Key.D1 + slotIndex,
                IsEnabled = Shortcut.DefaultEnabled,
                Action = () => _playerData.SetActiveSlot(_ecsContext.World.DataStore, slotIndex),
            };
            
            shortcutService.RegisterShortcut(shortcut);
        }
        
        inputService.Scrolled += OnScrolled;
    }

    private void OnScrolled(object? sender, ScrolledEventArgs e)
    {
        if (_inputService.IsKeyHeld(Key.Shift))
        {
            return;
        }
        
        double scrollDelta = Math.Round(e.Delta, MidpointRounding.AwayFromZero);
        
        int activeSlot = _playerData.GetActiveSlot(_ecsContext.World.DataStore);
        activeSlot -= (int)scrollDelta;
        activeSlot = MathS.WrapInt(activeSlot, 0, SLOT_COUNT - 1);
        
        _playerData.SetActiveSlot(_ecsContext.World.DataStore, activeSlot);
    }

    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        if (WaywardBeyond.GameState != GameState.Playing)
        {
            return Result.FromSuccess();
        }
        
        Result<InventoryComponent> inventoryResult = _playerData.GetInventory(_ecsContext.World.DataStore);
        Result<int> activeSlotResult = _playerData.GetActiveSlot(_ecsContext.World.DataStore);
        if (!inventoryResult || !activeSlotResult)
        {
            return Result.FromSuccess();
        }

        InventoryComponent inventory = inventoryResult.Value;
        int activeSlot = activeSlotResult.Value;

        //  Hotbar
        using (ui.Element())
        {
            ui.Color = _backgroundColor;
            ui.Spacing = 8;
            ui.Padding = new Padding(
                left: 8,
                top: 8,
                right: 8,
                bottom: 8
            );
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Bottom,
                X = new Relative(0.5f),
                Y = new Relative(0.99f),
            };

            for (var slotIndex = 0; slotIndex < SLOT_COUNT; slotIndex++)
            {
                ItemStack itemStack = inventory.Contents?.Length > slotIndex ? inventory.Contents[slotIndex] : default;
                
                //  Slot
                using (ui.Element())
                {
                    ui.LayoutDirection = LayoutDirection.None;
                    ui.Padding = new Padding(left: 4, top: 4, right: 4, bottom: 4);
                    ui.Color = activeSlot == slotIndex ? _selectedColor : _slotColor;
                    ui.Constraints = new Constraints
                    {
                        Width = new Fixed(48),
                        Height = new Fixed(48),
                    };
                    
                    //  Slot number
                    int slotNumber = slotIndex + 1;
                    using (ui.Text(slotNumber.ToString()))
                    {
                        ui.FontSize = 16;
                    }

                    if (itemStack.Count == 0 || itemStack.ID == null)
                    {
                        continue;
                    }
                    
                    Result<Item> itemResult = _itemDatabase.Get(itemStack.ID);

                    //  Stack size
                    using (ui.Text(itemStack.Count.ToString()))
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
                    Material icon = itemResult.Success ? itemResult.Value.Icon : null!;
                    using (ui.Image(icon))
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
        }
        
        return Result.FromSuccess();
    }
}