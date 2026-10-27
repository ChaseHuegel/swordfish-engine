using System;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Items;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Config;
using WaywardBeyond.Items;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Renders contextual keycap control hints in the bottom-right corner above HUD and modal layers.</summary>
internal sealed class ControlHints : IUILayer
{
    private readonly struct HintEntry(string key, string action)
    {
        public readonly string Key = key;
        public readonly string Action = action;
    }

    private readonly OrientationSelector _orientationSelector;
    private readonly ShapeSelector _shapeSelector;
    private readonly Inventory _inventory;
    private readonly PlayerData _playerData;
    private readonly IECSContext _ecsContext;
    private readonly GameplayConfig _gameplayConfig;

    private readonly HintEntry _feedbackHint;

    private readonly HintEntry _changeShapeHint;
    private readonly HintEntry _changeOrientationHint;
    private readonly HintEntry _snapBrickHint;
    private readonly HintEntry _pickBrickHint;
    private readonly HintEntry _inventoryHint;

    private readonly HintEntry _inventoryMoveHint;
    private readonly HintEntry _inventorySplitHint;
    private readonly HintEntry _inventoryQuickMoveHint;
    private readonly HintEntry _inventoryHotbarHint;
    private readonly HintEntry _inventoryCloseHint;

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

        _feedbackHint = ParseHint(localization.GetString("ui.hint.feedback"));
        
        _changeShapeHint = ParseHint(localization.GetString("ui.hint.changeShape"));
        _changeOrientationHint = ParseHint(localization.GetString("ui.hint.changeOrientation"));
        _snapBrickHint = ParseHint(localization.GetString("ui.hint.snapBrick"));
        _pickBrickHint = ParseHint(localization.GetString("ui.hint.pickBrick"));
        _inventoryHint = ParseHint(localization.GetString("ui.hint.inventory"));

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
        using (ui.Element())
        {
            ui.Passthrough = true;
            ui.Spacing = 6;
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Bottom | Anchors.Right,
                X = new Fixed(-20),
                Y = new Fixed(-20),
            };

            if (_inventory.IsVisible())
            {
                RenderInventoryHints(ui);
                return Result.FromSuccess();
            }

            Result<ItemSlot> mainHandResult = _playerData.GetMainHand(_ecsContext.World.DataStore);
            bool holdingPlaceable = mainHandResult.Success && mainHandResult.Value.Item.Placeable != null;

            if (holdingPlaceable)
            {
                RenderPlaceableHints(ui);
            }
            else
            {
                RenderIdleHints(ui);
            }
        }

        return Result.FromSuccess();
    }

    /// <summary>Draws the hints that belong to the open inventory.</summary>
    private void RenderInventoryHints(UIBuilder<Material> ui)
    {
        RenderHintPill(ui, in _inventoryMoveHint);
        RenderHintPill(ui, in _inventorySplitHint);
        RenderHintPill(ui, in _inventoryQuickMoveHint);
        RenderHintPill(ui, in _inventoryHotbarHint);
        RenderHintPill(ui, in _inventoryCloseHint);
        RenderHintPill(ui, in _feedbackHint);
    }

    /// <summary>Draws the hints for a placeable item in the main hand.</summary>
    private void RenderPlaceableHints(UIBuilder<Material> ui)
    {
        if (_shapeSelector.Available)
        {
            RenderHintPill(ui, in _changeShapeHint);
        }

        if (_orientationSelector.Available)
        {
            RenderHintPill(ui, in _changeOrientationHint);
        }

        RenderHintPill(ui, in _snapBrickHint);
        RenderHintPill(ui, in _pickBrickHint);
        RenderHintPill(ui, in _inventoryHint);
        RenderHintPill(ui, in _feedbackHint);
    }

    /// <summary>Draws the hints for a main hand that holds no placeable item.</summary>
    private void RenderIdleHints(UIBuilder<Material> ui)
    {
        RenderHintPill(ui, in _inventoryHint);
        RenderHintPill(ui, in _feedbackHint);
    }

    /// <summary>Draws one hint pill with its optional keycap.</summary>
    private static void RenderHintPill(UIBuilder<Material> ui, in HintEntry hint)
    {
        if (string.IsNullOrEmpty(hint.Action))
        {
            return;
        }

        using (ui.Element())
        {
            ui.Passthrough = true;
            ui.Spacing = 8;
            ui.Padding = new Padding(left: 4, top: 4, right: 10, bottom: 4);
            ui.LayoutDirection = LayoutDirection.Horizontal;
            ui.Color = UiTheme.PanelBackground;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
                Color = UiTheme.Border,
            };
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Right,
            };

            if (!string.IsNullOrEmpty(hint.Key))
            {
                RenderKeyBadge(ui, hint.Key);
            }

            using (ui.Text(hint.Action, UiMetrics.TINY_FONT))
            {
                ui.Passthrough = true;
                ui.Color = UiTheme.Text;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };
            }
        }
    }

    /// <summary>Draws the keycap chip of a hint pill.</summary>
    private static void RenderKeyBadge(UIBuilder<Material> ui, string key)
    {
        using (ui.Element())
        {
            ui.Passthrough = true;
            ui.Padding = new Padding(left: 6, top: 2, right: 6, bottom: 2);
            ui.Color = UiTheme.AccentDark;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
                Color = UiTheme.BorderHover,
            };
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };

            using (ui.Text(key, UiMetrics.TINY_FONT))
            {
                ui.Passthrough = true;
                ui.Color = UiTheme.AccentBright;
            }
        }
    }

    private static HintEntry ParseHint(string? rawHint)
    {
        if (string.IsNullOrWhiteSpace(rawHint))
        {
            return default;
        }

        int separatorIndex = rawHint.IndexOf(": ", StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            return new HintEntry(string.Empty, rawHint.Trim());
        }

        string key = rawHint[..separatorIndex].Trim();
        string action = rawHint[(separatorIndex + 2)..].Trim();
        return new HintEntry(key, action);
    }
}
