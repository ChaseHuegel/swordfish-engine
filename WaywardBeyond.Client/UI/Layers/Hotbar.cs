using System;
using System.Globalization;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Extensions;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Client.Items;
using WaywardBeyond.Items;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.UI.Layers;

internal class Hotbar
{
    public const int SLOT_COUNT = 9;

    private readonly IAssetDatabase<Item> _itemDatabase;
    private readonly ItemIconDatabase _itemIcons;
    private readonly PlayerData _playerData;
    private readonly IECSContext _ecsContext;
    private readonly InteractionState _interactionState;
    private readonly ControlSettings _controlSettings;


    public Hotbar(
        in IShortcutService shortcutService,
        in IInputService inputService,
        in IAssetDatabase<Item> itemDatabase,
        in ItemIconDatabase itemIcons,
        in PlayerData playerData,
        in IECSContext ecsContext,
        in InteractionState interactionState,
        in ControlSettings controlSettings
    ) {
        _itemDatabase = itemDatabase;
        _itemIcons = itemIcons;
        _playerData = playerData;
        _ecsContext = ecsContext;
        _interactionState = interactionState;
        _controlSettings = controlSettings;

        
        for (var i = 0; i < SLOT_COUNT; i++)
        {
            int slotNumber = i + 1;
            int slotIndex = i;
            var shortcut = new Shortcut
            {
                Name = $"Slot {slotNumber}",
                Category = "Inventory",
                Modifiers = ShortcutModifiers.None,
                Key = Key.D1 + slotIndex,
                IsEnabled = IsInputAllowed,
                Action = () => SetActiveSlot(slotIndex),
            };
            
            shortcutService.RegisterShortcut(shortcut);
        }
        
        inputService.Scrolled += OnScrolled;
    }

    public bool IsVisible()
    {
        return true;
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
        int activeSlot = activeSlotResult.Value;
        
        //  Hotbar
        using (ui.Element())
        {
            ui.Color = UiTheme.SlotBackground;
            ui.Spacing = 8;
            ui.Padding = new Padding(
                left: 8,
                top: 8,
                right: 8,
                bottom: 8
            );

            for (var slotIndex = 0; slotIndex < SLOT_COUNT; slotIndex++)
            {
                ItemData itemStack = inventory.Contents?.Length > slotIndex ? inventory.Contents[slotIndex] : default;
                
                //  Slot
                using (ui.Element())
                {
                    ui.LayoutDirection = LayoutDirection.None;
                    ui.Padding = new Padding(left: 2, top: 2, right: 2, bottom: 2);
                    ui.Color = activeSlot == slotIndex ? UiTheme.SlotSelected : UiTheme.Slot;
                    ui.Constraints = new Constraints
                    {
                        Width = new Fixed(48),
                        Height = new Fixed(48),
                    };
                    
                    //  Icon
                    if (itemStack.Count > 0 && itemStack.ID != null)
                    {
                        Material icon = _itemIcons.Get(itemStack.ID);
                        using (ui.Image(icon))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                                Width = new Relative(1f),
                                Height = new Relative(1f),
                            };
                        }
                    }

                    //  Slot number
                    int slotNumber = slotIndex + 1;
                    using (ui.Text(slotNumber.ToString()))
                    {
                        ui.FontSize = UiMetrics.SmallFont;
                    }

                    if (itemStack.Count == 0 || itemStack.ID == null)
                    {
                        continue;
                    }
                    
                    //  Stack size
                    using (ui.Text(itemStack.Count.ToString()))
                    {
                        ui.FontSize = UiMetrics.SmallFont;
                        ui.Constraints = new Constraints
                        {
                            Anchors = Anchors.Bottom | Anchors.Right,
                        };
                    }
                }
            }
        }
        
        return Result.FromSuccess();
    }
    
    private bool IsInputAllowed()
    {
        return WaywardBeyond.GameState == GameState.Playing && !_interactionState.IsInteractionBlocked();
    }
    
    private void OnScrolled(object? sender, ScrolledEventArgs e)
    {
        if (!IsInputAllowed())
        {
            return;
        }
        
        double scrollDelta = Math.Round(e.Delta, MidpointRounding.AwayFromZero);
        scrollDelta = Math.Clamp(scrollDelta, -1f, 1f);
        int steps = (int)scrollDelta;
        if (steps == 0)
        {
            return;
        }
        
        int activeSlot = _playerData.GetActiveSlot(_ecsContext.World.DataStore);
        activeSlot -= steps;
        activeSlot = MathS.WrapInt(activeSlot, 0, SLOT_COUNT - 1);
        
        SetActiveSlot(activeSlot);
    }

    private void SetActiveSlot(int activeSlot)
    {
        _playerData.SetActiveSlot(_ecsContext.World.DataStore, activeSlot);

        if (!_controlSettings.RememberShape.Get())
        {
            _interactionState.SelectedShape.Set(BrickShape.Block);
        }
        
        if (!_controlSettings.RememberOrientation.Get())
        {
            _interactionState.SelectedOrientation.Set(Orientation.Identity);
        }
    }
}