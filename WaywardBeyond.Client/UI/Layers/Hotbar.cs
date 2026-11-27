using System;
using System.Collections.Generic;
using System.Threading;
using Krill;
using Reef;
using Swordfish.ECS;
using Swordfish.Graphics;
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
using WaywardBeyond.Items;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Renders the nine-slot bottom equipment hotbar and processes slot selection shortcuts.</summary>
internal sealed class Hotbar
{
    private readonly HotbarView _view = new();

    /// <summary>The number of equipped hotbar slots.</summary>
    public const int SLOT_COUNT = 9;

    private readonly IShortcutService _shortcutService;
    private readonly ItemIconDatabase _itemIcons;
    private readonly PlayerData _playerData;
    private readonly IECSContext _ecsContext;
    private readonly InteractionState _interactionState;
    private readonly ControlSettings _controlSettings;
    private readonly string[] _slotLabels = new string[SLOT_COUNT];
    private readonly IInputService _inputService;
    private int _slotLabelsDirty = 1;

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
        _shortcutService = shortcutService;
        _itemIcons = itemIcons;
        _playerData = playerData;
        _ecsContext = ecsContext;
        _interactionState = interactionState;
        _controlSettings = controlSettings;
        _inputService = inputService;

        for (var i = 0; i < SLOT_COUNT; i++)
        {
            int slotIndex = i;
            GameShortcuts.RegisterOrAddHandler(
                shortcutService,
                GameShortcuts.CreateSlot(slotIndex),
                IsInputAllowed,
                () => SetActiveSlot(slotIndex)
            );
        }

        _shortcutService.BindingsChanged += OnBindingsChanged;
        GameShortcuts.ResolveSlotLabels(_shortcutService, _slotLabels);
        _slotLabelsDirty = 0;

        inputService.Scrolled += OnScrolled;
    }

    /// <summary>Returns whether the hotbar is visible.</summary>
    public bool IsVisible() => true;

    /// <summary>Renders the hotbar container and its nine item slots.</summary>
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

        var slots = new List<ItemSlotState>(SLOT_COUNT);
        for (var slotIndex = 0; slotIndex < SLOT_COUNT; slotIndex++)
        {
            ItemData itemStack = default;
            if (inventory.Contents != null && slotIndex < inventory.Contents.Length)
            {
                itemStack = inventory.Contents[slotIndex];
            }

            var hasItem = itemStack.Count > 0 && itemStack.ID != null;
            Material? icon = hasItem ? _itemIcons.Get(itemStack.ID!) : null;
            slots.Add(new ItemSlotState(
                icon!,
                _slotLabels[slotIndex],
                hasItem ? itemStack.Count.ToString() : null,
                Selected: activeSlot == slotIndex
            ));
        }

        _view.Render(ui, new HotbarState(slots));

        return Result.FromSuccess();
    }

    private void OnBindingsChanged()
    {
        Interlocked.Exchange(ref _slotLabelsDirty, 1);
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

        if (_inputService.IsKeyHeld(Key.Shift))
        {
            Result<global::WaywardBeyond.Items.ItemSlot> mainHand = _playerData.GetMainHand(_ecsContext.World.DataStore);
            if (mainHand.Success
                && (mainHand.Value.Item.Tool?.Type == ToolType.Blueprint
                    || ItemPlaceables.IsBlueprintPlaceable(mainHand.Value.Item)))
            {
                return;
            }
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
