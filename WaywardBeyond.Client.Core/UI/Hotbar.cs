using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.UI.Reef;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Items;

namespace WaywardBeyond.Client.Core.UI;

internal class Hotbar : EntitySystem<PlayerComponent, InventoryComponent>
{
    private const int SLOT_COUNT = 9;

    private readonly IAssetDatabase<Item> _itemDatabase;
    private readonly ReefContext _reefContext;

    private readonly Lock _inventoryLock = new();
    private InventoryComponent? _inventory;

    public readonly DataBinding<int> ActiveSlot = new(0);
    
    public Hotbar(IWindowContext windowContext, ReefContext reefContext, IShortcutService shortcutService, IAssetDatabase<Item> itemDatabase)
    {
        _reefContext = reefContext;
        _itemDatabase = itemDatabase;
        
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
                Action = () => ActiveSlot.Set(slotIndex),
            };
            
            shortcutService.RegisterShortcut(shortcut);
        }
        
        windowContext.Update += OnWindowUpdate;
    }

    private void OnWindowUpdate(double delta)
    {
        InventoryComponent? inventory;
        lock (_inventoryLock)
        {
            inventory = _inventory;
        }

        UIBuilder<Material> ui = _reefContext.Builder;

        //  Hotbar
        using (ui.Element())
        {
            ui.Color = new Vector4(0.25f, 0.25f, 0.25f, 1f);
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
                ItemStack itemStack;
                if (inventory == null)
                {
                    itemStack = default;
                }
                else
                {
                    itemStack = inventory.Value.Contents.Length > slotIndex ? inventory.Value.Contents[slotIndex] : default;
                }
                
                //  Slot
                using (ui.Element())
                {
                    ui.LayoutDirection = LayoutDirection.None;
                    ui.Padding = new Padding(left: 4, top: 4, right: 4, bottom: 4);
                    ui.Color = ActiveSlot == slotIndex ? new Vector4(0f, 1f, 0.5f, 1f) : new Vector4(0f, 0.5f, 0.5f, 1f);
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
                    
                    //  Name
                    string name = itemResult.Success ? itemResult.Value.Name : itemStack.ID;
                    using (ui.Text(name))
                    {
                        ui.FontSize = 12;
                        ui.Constraints = new Constraints
                        {
                            Anchors = Anchors.Center,
                            X = new Relative(0.5f),
                            Y = new Relative(0.5f),
                        };
                    }

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
    }
    
    protected override void OnTick(float delta, DataStore store, int entity, ref PlayerComponent player, ref InventoryComponent inventory)
    {
        lock (_inventoryLock)
        {
            _inventory = inventory;
        }
    }
}