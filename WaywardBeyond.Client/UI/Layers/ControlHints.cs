using System;
using System.Collections.Generic;
using System.Threading;
using Reef;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Shortcuts;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Config;
using WaywardBeyond.Items;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Renders contextual keycap control hints in the bottom-right corner above HUD and modal layers.</summary>
internal sealed class ControlHints : IUILayer
{
    private readonly ControlHintsView _view = new();

    private readonly OrientationSelector _orientationSelector;
    private readonly ShapeSelector _shapeSelector;
    private readonly PaintColorSelector _paintColorSelector;
    private readonly PaintTextureSelector _paintTextureSelector;
    private readonly Inventory _inventory;
    private readonly PlayerData _playerData;
    private readonly IECSContext _ecsContext;
    private readonly ILocalization _localization;
    private readonly GameplayConfig _gameplayConfig;
    private readonly IShortcutService _shortcutService;
    private readonly RegionToolState? _regionToolState;

    private HintViewState _feedbackHint;
    private HintViewState _changeShapeHint;
    private HintViewState _changeOrientationHint;
    private HintViewState _snapBrickHint;
    private HintViewState _pickBrickHint;
    private HintViewState _changeColorHint;
    private HintViewState _changeTextureHint;
    private HintViewState _regionCornerAHint;
    private HintViewState _regionCornerBHint;
    private HintViewState _regionCopyHint;
    private HintViewState _regionCutHint;
    private HintViewState _regionPasteModeHint;
    private HintViewState _regionPasteHint;
    private HintViewState _regionCancelPasteHint;
    private HintViewState _regionRotateHint;
    private HintViewState _regionMirrorHint;
    private HintViewState _regionOverwriteAirHint;
    private HintViewState _regionDistanceHint;
    private HintViewState _regionEmbedHint;
    private HintViewState _regionRadialHint;
    private HintViewState _regionLibraryHint;
    private HintViewState _inventoryHint;
    private HintViewState _inventoryMoveHint;
    private HintViewState _inventorySplitHint;
    private HintViewState _inventoryQuickMoveHint;
    private HintViewState _inventoryHotbarHint;
    private HintViewState _inventoryCloseHint;
    private List<HintViewState> _projectorSelectHints = [];
    private List<HintViewState> _projectorPasteHints = [];
    private int _hintsDirty = 1;

    /// <summary>Initializes a new instance of the <see cref="ControlHints"/> class.</summary>
    public ControlHints(
        in OrientationSelector orientationSelector,
        in ShapeSelector shapeSelector,
        in PaintColorSelector paintColorSelector,
        in PaintTextureSelector paintTextureSelector,
        in Inventory inventory,
        in PlayerData playerData,
        in IECSContext ecsContext,
        in ILocalization localization,
        in GameplayConfig gameplayConfig,
        in IShortcutService shortcutService,
        RegionToolState? regionToolState = null
    )
    {
        _orientationSelector = orientationSelector;
        _shapeSelector = shapeSelector;
        _paintColorSelector = paintColorSelector;
        _paintTextureSelector = paintTextureSelector;
        _inventory = inventory;
        _playerData = playerData;
        _ecsContext = ecsContext;
        _localization = localization;
        _gameplayConfig = gameplayConfig;
        _shortcutService = shortcutService;
        _regionToolState = regionToolState;

        _shortcutService.BindingsChanged += OnBindingsChanged;
        RefreshHints();
        _hintsDirty = 0;
    }

    /// <inheritdoc/>
    public bool IsVisible() => WaywardBeyond.GameState == GameState.Playing && _gameplayConfig.ControlHints.Get();

    /// <inheritdoc/>
    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        if (Interlocked.Exchange(ref _hintsDirty, 0) == 1)
        {
            RefreshHints();
        }

        List<HintViewState> hints;
        if (_inventory.IsVisible())
        {
            hints = BuildInventoryHints();
        }
        else
        {
            Result<ItemSlot> mainHandResult = _playerData.GetMainHand(_ecsContext.World.DataStore);
            bool holdingPlaceable = mainHandResult.Success && mainHandResult.Value.Item.Placeable != null;
            bool holdingProjector = mainHandResult.Success && mainHandResult.Value.Item.Tool?.Type == ToolType.Blueprint;
            if (holdingProjector)
            {
                hints = BuildProjectorHints();
            }
            else if (holdingPlaceable)
            {
                hints = BuildPlaceableHints();
            }
            else if (_paintColorSelector.Available)
            {
                hints = BuildSprayerHints();
            }
            else if (_paintTextureSelector.Available)
            {
                hints = BuildDrillHints();
            }
            else
            {
                hints = BuildIdleHints();
            }
        }

        _view.Render(ui, new ControlHintsState(hints));

        return Result.FromSuccess();
    }

    private void OnBindingsChanged()
    {
        Interlocked.Exchange(ref _hintsDirty, 1);
    }

    private void RefreshHints()
    {
        _feedbackHint = CreateDynamicHint(GameShortcuts.FEEDBACK_FORM, "ui.hint.feedback.send", "F8");
        _changeShapeHint = CreateDynamicHint(GameShortcuts.CHANGE_SHAPE, "ui.hint.brick.changeShape", "R");
        _changeOrientationHint = CreateDynamicHint(GameShortcuts.CHANGE_ORIENTATION, "ui.hint.brick.changeOrientation", "T");
        _snapBrickHint = CreateDynamicHint(GameShortcuts.SNAP_PLACEMENT, "ui.hint.brick.snap", "Shift");
        _pickBrickHint = CreateDynamicHint(GameShortcuts.PICK_BRICK, "ui.hint.brick.pick", "MMB");
        _changeColorHint = CreateDynamicHint(GameShortcuts.SELECT_PAINT_COLOR, "ui.hint.sprayer.changeColor", "R");
        _changeTextureHint = CreateDynamicHint(GameShortcuts.SELECT_PAINT_TEXTURE, "ui.hint.drill.changeTexture", "R");
        _regionCornerAHint = CreateDynamicHint(GameShortcuts.PRIMARY_ACTION, "ui.hint.region.cornerA", "LMB");
        _regionCornerBHint = CreateDynamicHint(GameShortcuts.SECONDARY_ACTION, "ui.hint.region.cornerB", "RMB");
        _regionCopyHint = new HintViewState("C", GetHintAction("ui.hint.region.copy"));
        _regionCutHint = new HintViewState("X", GetHintAction("ui.hint.region.cut"));
        _regionPasteModeHint = new HintViewState("V", GetHintAction("ui.hint.region.pasteMode"));
        _regionPasteHint = CreateDynamicHint(GameShortcuts.PRIMARY_ACTION, "ui.hint.region.paste", "LMB");
        _regionCancelPasteHint = CreateDynamicHint(GameShortcuts.SECONDARY_ACTION, "ui.hint.region.cancelPaste", "RMB");
        _regionRotateHint = new HintViewState("T", GetHintAction("ui.hint.region.rotate"));
        _regionMirrorHint = new HintViewState("F", GetHintAction("ui.hint.region.mirror"));
        _regionOverwriteAirHint = new HintViewState("G", GetHintAction("ui.hint.region.overwriteAir"));
        _regionDistanceHint = new HintViewState("Shift+Scroll", GetHintAction("ui.hint.region.distance"));
        _regionEmbedHint = CreateDynamicHint(GameShortcuts.SNAP_PLACEMENT, "ui.hint.region.embed", "Shift");
        _regionRadialHint = new HintViewState("R", GetHintAction("ui.hint.region.radial"));
        _regionLibraryHint = new HintViewState("B", GetHintAction("ui.hint.region.library"));
        _inventoryHint = CreateDynamicHint(GameShortcuts.INVENTORY, "ui.hint.inventory.toggle", "Tab");

        _inventoryMoveHint = CreateDynamicHint(GameShortcuts.INVENTORY_MOVE_STACK, "ui.hint.inventory.move", "LMB");
        _inventorySplitHint = CreateDynamicHint(GameShortcuts.INVENTORY_SPLIT_STACK, "ui.hint.inventory.split", "RMB");
        _inventoryQuickMoveHint = CreateDynamicHint(GameShortcuts.INVENTORY_QUICK_MOVE, "ui.hint.inventory.quickMove", "Shift+LMB");
        _inventoryHotbarHint = new HintViewState(FormatHotbarRangeKey(), GetHintAction("ui.hint.inventory.hotbar"));
        _inventoryCloseHint = new HintViewState(FormatInventoryCloseKey(), GetHintAction("ui.hint.inventory.close"));

        _projectorSelectHints =
        [
            _regionCornerAHint,
            _regionCornerBHint,
            _regionCopyHint,
            _regionCutHint,
            _regionPasteModeHint,
            _regionRadialHint,
            _regionLibraryHint,
        ];

        _projectorPasteHints =
        [
            _regionPasteHint,
            _regionCancelPasteHint,
            _regionRotateHint,
            _regionMirrorHint,
            _regionOverwriteAirHint,
            _regionEmbedHint,
            _regionDistanceHint,
            _regionRadialHint,
            _regionLibraryHint,
        ];
    }

    private HintViewState CreateDynamicHint(string shortcutId, string localizationKey, string fallbackKey)
    {
        string action = GetHintAction(localizationKey);
        string key = FormatBindingKey(shortcutId, fallbackKey);
        return new HintViewState(key, action);
    }

    private string GetHintAction(string localizationKey)
    {
        string? raw = _localization.GetString(localizationKey);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        int separatorIndex = raw.IndexOf(": ", StringComparison.Ordinal);
        return separatorIndex > 0 ? raw[(separatorIndex + 2)..].Trim() : raw.Trim();
    }

    private string FormatBindingKey(string shortcutId, string fallbackKey)
    {
        Result<ShortcutBinding> result = _shortcutService.GetBinding(shortcutId);
        if (!result.Success)
        {
            return fallbackKey;
        }

        return result.Value.IsBound ? result.Value.ToDisplayString() : string.Empty;
    }

    private string FormatHotbarRangeKey()
    {
        var anyResolved = false;
        var allDigits = true;
        var allNumpad = true;
        string? first = null;
        string? last = null;

        for (var i = 0; i < Hotbar.SLOT_COUNT; i++)
        {
            Result<ShortcutBinding> result = _shortcutService.GetBinding(GameShortcuts.Slot(i));
            if (!result.Success)
            {
                allDigits = false;
                allNumpad = false;
                continue;
            }

            anyResolved = true;
            ShortcutBinding binding = result.Value;
            if (!binding.IsKey || binding.Modifiers != ShortcutModifiers.None || binding.Key != Key.D1 + i)
            {
                allDigits = false;
            }

            if (!binding.IsKey || binding.Modifiers != ShortcutModifiers.None || binding.Key != Key.Numpad1 + i)
            {
                allNumpad = false;
            }

            if (binding.IsBound)
            {
                string display = binding.ToDisplayString();
                first ??= display;
                last = display;
            }
        }

        if (!anyResolved || allDigits)
        {
            return "1-9";
        }

        if (allNumpad)
        {
            return "Num 1-9";
        }

        if (first == null)
        {
            return string.Empty;
        }

        return string.Equals(first, last, StringComparison.Ordinal) ? first : $"{first}-{last}";
    }

    private string FormatInventoryCloseKey()
    {
        string toggle = FormatBindingKey(GameShortcuts.INVENTORY, "Tab");
        string back = FormatBindingKey(GameShortcuts.BACK, "Esc");
        if (toggle.Length == 0)
        {
            return back;
        }

        if (back.Length == 0 || string.Equals(toggle, back, StringComparison.Ordinal))
        {
            return toggle;
        }

        return $"{toggle} / {back}";
    }

    /// <summary>Builds the hints that belong to the open inventory.</summary>
    private List<HintViewState> BuildInventoryHints()
    {
        return
        [
            _inventoryMoveHint,
            _inventorySplitHint,
            _inventoryQuickMoveHint,
            _inventoryHotbarHint,
            _inventoryCloseHint,
            _feedbackHint,
        ];
    }

    /// <summary>Builds the hints for a placeable item in the main hand.</summary>
    private List<HintViewState> BuildPlaceableHints()
    {
        var hints = new List<HintViewState>();
        if (_shapeSelector.Available)
        {
            hints.Add(_changeShapeHint);
        }

        if (_orientationSelector.Available)
        {
            hints.Add(_changeOrientationHint);
        }

        hints.Add(_snapBrickHint);
        hints.Add(_pickBrickHint);
        hints.Add(_inventoryHint);
        hints.Add(_feedbackHint);
        return hints;
    }

    /// <summary>Builds the hints for the paint sprayer in the main hand.</summary>
    private List<HintViewState> BuildSprayerHints()
    {
        return [_changeColorHint, _inventoryHint, _feedbackHint];
    }

    /// <summary>Builds the hints for the drill in the main hand.</summary>
    private List<HintViewState> BuildDrillHints()
    {
        return [_changeTextureHint, _inventoryHint, _feedbackHint];
    }

    /// <summary>Builds the hints for the Blueprint Projector in Select or Paste mode.</summary>
    private List<HintViewState> BuildProjectorHints()
    {
        return _regionToolState?.Mode == RegionToolMode.Paste
            ? _projectorPasteHints
            : _projectorSelectHints;
    }

    /// <summary>Builds the hints for a main hand that holds no placeable item.</summary>
    private List<HintViewState> BuildIdleHints()
    {
        return [_inventoryHint, _feedbackHint];
    }
}
