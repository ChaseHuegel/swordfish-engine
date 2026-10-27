using System;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Items;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Renders the nine-slot bottom equipment hotbar and processes slot selection shortcuts.</summary>
internal sealed class Hotbar
{
    /// <summary>The number of equipped hotbar slots.</summary>
    public const int SLOT_COUNT = 9;

    private static readonly string[] _slotNumbers = ["1", "2", "3", "4", "5", "6", "7", "8", "9"];

    private readonly ItemIconDatabase _itemIcons;
    private readonly PlayerData _playerData;
    private readonly IECSContext _ecsContext;
    private readonly InteractionState _interactionState;
    private readonly ControlSettings _controlSettings;

    /// <summary>Initializes a new instance of the <see cref="Hotbar"/> class.</summary>
    public Hotbar(
        in IShortcutService shortcutService,
        in IInputService inputService,
        in ItemIconDatabase itemIcons,
        in PlayerData playerData,
        in IECSContext ecsContext,
        in InteractionState interactionState,
        in ControlSettings controlSettings
    )
    {
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

    /// <summary>Returns whether the hotbar is visible.</summary>
    public bool IsVisible() => true;

    /// <summary>Renders the hotbar container and its nine item slots.</summary>
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

        using (ui.Element())
        {
            ui.Passthrough = true;
            ui.Color = UiTheme.PanelBackground;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
                Color = UiTheme.Border,
            };
            ui.CornerCaps = new CornerCapStyle
            {
                Length = UiMetrics.PANEL_CAP_LENGTH,
                Thickness = UiMetrics.PANEL_CAP_THICKNESS,
                Color = UiTheme.BorderHover,
            };
            ui.Spacing = 6;
            ui.Padding = new Padding(left: 8, top: 8, right: 8, bottom: 8);
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };

            DrawSlots(ui, in inventory, activeSlot);
        }

        return Result.FromSuccess();
    }

    /// <summary>Draws the nine item slots in order.</summary>
    private void DrawSlots(UIBuilder<Material> ui, in InventoryComponent inventory, int activeSlot)
    {
        for (var slotIndex = 0; slotIndex < SLOT_COUNT; slotIndex++)
        {
            ItemData itemStack = default;
            if (inventory.Contents != null && slotIndex < inventory.Contents.Length)
            {
                itemStack = inventory.Contents[slotIndex];
            }

            DrawSlot(ui, in itemStack, slotIndex, activeSlot == slotIndex);
        }
    }

    /// <summary>Draws one slot's frame, icon, number, and stack count.</summary>
    private void DrawSlot(UIBuilder<Material> ui, in ItemData itemStack, int slotIndex, bool isSelected)
    {
        using (ui.Element())
        {
            ui.Passthrough = true;
            ui.LayoutDirection = LayoutDirection.None;
            ui.Padding = new Padding(left: 4, top: 4, right: 4, bottom: 4);
            ApplySlotStyle(ui, isSelected);
            ui.Constraints = new Constraints
            {
                Width = new Fixed(48),
                Height = new Fixed(48),
            };

            if (itemStack.Count > 0 && itemStack.ID != null)
            {
                DrawSlotIcon(ui, in itemStack);
            }

            using (ui.Text(_slotNumbers[slotIndex], UiMetrics.TINY_FONT))
            {
                ui.Passthrough = true;
                ui.Color = isSelected ? UiTheme.PrimaryBright : UiTheme.TextDim;
            }

            if (itemStack.Count == 0 || itemStack.ID == null)
            {
                return;
            }

            using (ui.Text(itemStack.Count.ToString(), UiMetrics.TINY_FONT))
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

    /// <summary>Draws the item icon that fills a slot.</summary>
    private void DrawSlotIcon(UIBuilder<Material> ui, in ItemData itemStack)
    {
        Material icon = _itemIcons.Get(itemStack.ID!);
        using (ui.Image(icon))
        {
            ui.Passthrough = true;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Relative(1f),
                Height = new Relative(1f),
            };
        }
    }

    /// <summary>Applies the selected or unselected frame style to a slot.</summary>
    private static void ApplySlotStyle(UIBuilder<Material> ui, bool isSelected)
    {
        if (isSelected)
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
