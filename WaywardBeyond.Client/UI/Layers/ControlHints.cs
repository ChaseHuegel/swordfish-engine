using System;
using System.Collections.Generic;
using Reef;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Config;
using WaywardBeyond.Items;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Renders contextual keycap control hints in the bottom-right corner above HUD and modal layers.</summary>
internal sealed class ControlHints : IUILayer
{
    private readonly OrientationSelector _orientationSelector;
    private readonly ShapeSelector _shapeSelector;
    private readonly Inventory _inventory;
    private readonly PlayerData _playerData;
    private readonly IECSContext _ecsContext;
    private readonly GameplayConfig _gameplayConfig;

    private readonly HintViewState _feedbackHint;

    private readonly HintViewState _changeShapeHint;
    private readonly HintViewState _changeOrientationHint;
    private readonly HintViewState _snapBrickHint;
    private readonly HintViewState _pickBrickHint;
    private readonly HintViewState _inventoryHint;

    private readonly HintViewState _inventoryMoveHint;
    private readonly HintViewState _inventorySplitHint;
    private readonly HintViewState _inventoryQuickMoveHint;
    private readonly HintViewState _inventoryHotbarHint;
    private readonly HintViewState _inventoryCloseHint;

    /// <summary>Initializes a new instance of the <see cref="ControlHints"/> class.</summary>
    public ControlHints(
        in OrientationSelector orientationSelector,
        in ShapeSelector shapeSelector,
        in Inventory inventory,
        in PlayerData playerData,
        in IECSContext ecsContext,
        in ILocalization localization,
        in GameplayConfig gameplayConfig
    )
    {
        _orientationSelector = orientationSelector;
        _shapeSelector = shapeSelector;
        _inventory = inventory;
        _playerData = playerData;
        _ecsContext = ecsContext;
        _gameplayConfig = gameplayConfig;

        _feedbackHint = ParseHint(localization.GetString("ui.hint.feedback.send"));
        
        _changeShapeHint = ParseHint(localization.GetString("ui.hint.brick.changeShape"));
        _changeOrientationHint = ParseHint(localization.GetString("ui.hint.brick.changeOrientation"));
        _snapBrickHint = ParseHint(localization.GetString("ui.hint.brick.snap"));
        _pickBrickHint = ParseHint(localization.GetString("ui.hint.brick.pick"));
        _inventoryHint = ParseHint(localization.GetString("ui.hint.inventory.toggle"));

        _inventoryMoveHint = ParseHint(localization.GetString("ui.hint.inventory.move"));
        _inventorySplitHint = ParseHint(localization.GetString("ui.hint.inventory.split"));
        _inventoryQuickMoveHint = ParseHint(localization.GetString("ui.hint.inventory.quickMove"));
        _inventoryHotbarHint = ParseHint(localization.GetString("ui.hint.inventory.hotbar"));
        _inventoryCloseHint = ParseHint(localization.GetString("ui.hint.inventory.close"));
    }

    /// <inheritdoc/>
    public bool IsVisible() => WaywardBeyond.GameState == GameState.Playing && _gameplayConfig.ControlHints.Get();

    /// <inheritdoc/>
    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        List<HintViewState> hints;
        if (_inventory.IsVisible())
        {
            hints = BuildInventoryHints();
        }
        else
        {
            Result<ItemSlot> mainHandResult = _playerData.GetMainHand(_ecsContext.World.DataStore);
            bool holdingPlaceable = mainHandResult.Success && mainHandResult.Value.Item.Placeable != null;
            hints = holdingPlaceable ? BuildPlaceableHints() : BuildIdleHints();
        }

        new ControlHintsView().Render(ui, new ControlHintsState(hints));

        return Result.FromSuccess();
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

    /// <summary>Builds the hints for a main hand that holds no placeable item.</summary>
    private List<HintViewState> BuildIdleHints()
    {
        return [_inventoryHint, _feedbackHint];
    }

    private static HintViewState ParseHint(string? rawHint)
    {
        if (string.IsNullOrWhiteSpace(rawHint))
        {
            return default;
        }

        int separatorIndex = rawHint.IndexOf(": ", StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            return new HintViewState(string.Empty, rawHint.Trim());
        }

        string key = rawHint[..separatorIndex].Trim();
        string action = rawHint[(separatorIndex + 2)..].Trim();
        return new HintViewState(key, action);
    }
}
