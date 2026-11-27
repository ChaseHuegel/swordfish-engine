using System;
using System.Collections.Generic;
using System.Numerics;
using Reef;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Shortcuts;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI.Layers.Blueprints;
using WaywardBeyond.Items;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Renders the radial region action picker when holding R with the Blueprint Projector.</summary>
internal sealed class RegionToolRadial : IUILayer, IActionIndicator
{
    private const int INDEX_SELECT = 0;
    private const int INDEX_COPY = 1;
    private const int INDEX_CUT = 2;
    private const int INDEX_PASTE = 3;
    private const int INDEX_ROTATE = 4;
    private const int INDEX_MIRROR = 5;
    private const int INDEX_OVERWRITE_AIR = 6;
    private const int INDEX_BLUEPRINTS = 7;

    private static readonly string[] _labelKeys =
    [
        "ui.label.region.select",
        "ui.label.region.copy",
        "ui.label.region.cut",
        "ui.label.region.paste",
        "ui.label.region.rotate",
        "ui.label.region.mirror",
        "ui.label.region.overwriteAir",
        "ui.label.region.blueprints",
    ];

    private static readonly string[] _iconPaths =
    [
        "ui/region_select.png",
        "ui/region_copy.png",
        "ui/region_cut.png",
        "ui/region_paste.png",
        "ui/region_rotate.png",
        "ui/region_mirror.png",
        "ui/region_overwrite_air.png",
        "ui/region_blueprints.png",
    ];

    private readonly RadialMenuView _view;
    private readonly InteractionState _interactionState;
    private readonly PlayerData _playerData;
    private readonly IECSContext _ecsContext;
    private readonly RegionToolState _toolState;
    private readonly RegionToolService _toolService;
    private readonly Lazy<BlueprintLibraryLayer> _blueprintLibrary;
    private readonly Material[] _icons;

    private bool _selecting;
    private int _hoveredIndex = -1;
    private InteractionState.InteractionBlocker? _interactionBlocker;

    /// <summary>Gets whether the currently held item is the Blueprint Projector.</summary>
    public bool Available => IsHoldingProjector();

    /// <summary>Initializes a new instance of the <see cref="RegionToolRadial"/> class.</summary>
    public RegionToolRadial(
        in IShortcutService shortcutService,
        in IAssetDatabase<Texture> textureDatabase,
        in IAssetDatabase<Shader> shaderDatabase,
        in InteractionState interactionState,
        in PlayerData playerData,
        in IECSContext ecsContext,
        in ILocalization localization,
        in RegionToolState toolState,
        in RegionToolService toolService,
        Lazy<BlueprintLibraryLayer> blueprintLibrary
    )
    {
        _interactionState = interactionState;
        _playerData = playerData;
        _ecsContext = ecsContext;
        _toolState = toolState;
        _toolService = toolService;
        _blueprintLibrary = blueprintLibrary;
        _view = new RadialMenuView(localization);

        Result<Shader> shader = shaderDatabase.Get("ui_reef_textured");
        var fallbackTexture = new Texture("region_tool_fallback", [220, 220, 220, 255], 1, 1, mipmaps: false);
        var fallbackMaterial = new Material(shader, fallbackTexture);

        _icons = new Material[_iconPaths.Length];
        for (var i = 0; i < _iconPaths.Length; i++)
        {
            Result<Texture> tex = textureDatabase.Get(_iconPaths[i]);
            _icons[i] = tex.Success ? new Material(shader, tex) : fallbackMaterial;
        }

        shortcutService.RegisterShortcut(new Shortcut
        {
            Name = "Region tool actions",
            Category = "Interaction",
            Modifiers = ShortcutModifiers.None,
            Key = Key.R,
            SharedBindingGroup = GameShortcuts.TOOL_RADIAL_GROUP,
            IsEnabled = Shortcut.DefaultEnabled,
            Action = OnPressed,
            Released = OnReleased,
        });
    }

    /// <inheritdoc/>
    bool IUILayer.IsVisible() => _selecting;

    /// <inheritdoc/>
    bool IActionIndicator.IsVisible() => Available;

    /// <inheritdoc/>
    Material? IActionIndicator.IndicatorIcon =>
        _toolState.Mode == RegionToolMode.Paste ? _icons[INDEX_PASTE] : _icons[INDEX_SELECT];

    /// <inheritdoc/>
    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        if (!Available)
        {
            CancelWithoutDispatch();
            return Result.FromSuccess();
        }

        RadialMenuLayoutResult layout = RadialMenuLayout.Compute(_icons.Length, ui.Width, ui.Height);
        var nodes = new List<RadialNodeState>(_icons.Length);

        for (var i = 0; i < _icons.Length; i++)
        {
            Vector2 offset = layout.Positions[i];
            nodes.Add(new RadialNodeState(
                $"regionToolRadial{i + 1}",
                i,
                (int)offset.X,
                (int)offset.Y,
                _icons[i],
                IsNodeSelected(i)));
        }

        string labelKey = _hoveredIndex >= 0 && _hoveredIndex < _labelKeys.Length
            ? _labelKeys[_hoveredIndex]
            : (_toolState.Mode == RegionToolMode.Paste ? _labelKeys[INDEX_PASTE] : _labelKeys[INDEX_SELECT]);

        _view.Render(ui, new RadialMenuState(labelKey, nodes, OnNodeEnter, layout.HitSize));
        return Result.FromSuccess();
    }

    internal void OnNodeEnter(int index)
    {
        _hoveredIndex = index;
    }

    internal void OnPressed()
    {
        if (!Available || !_interactionState.TryBlockInteractionExclusive(out _interactionBlocker))
        {
            return;
        }

        _hoveredIndex = -1;
        _selecting = true;
    }

    internal void OnReleased()
    {
        if (!_selecting)
        {
            return;
        }

        int selectedIndex = _hoveredIndex;
        CancelWithoutDispatch();
        DispatchNodeAction(selectedIndex);
    }

    private void CancelWithoutDispatch()
    {
        _hoveredIndex = -1;
        _selecting = false;
        _interactionBlocker?.Dispose();
        _interactionBlocker = null;
    }

    private void DispatchNodeAction(int index)
    {
        switch (index)
        {
            case INDEX_SELECT:
                _toolService.QueueAction(RegionToolAction.SelectMode);
                break;
            case INDEX_COPY:
                _toolService.QueueAction(RegionToolAction.Copy);
                break;
            case INDEX_CUT:
                _toolService.QueueAction(RegionToolAction.Cut);
                break;
            case INDEX_PASTE:
                _toolService.QueueAction(RegionToolAction.TogglePasteMode);
                break;
            case INDEX_ROTATE:
                _toolService.QueueAction(RegionToolAction.RotateYaw);
                break;
            case INDEX_MIRROR:
                _toolService.QueueAction(RegionToolAction.MirrorHorizontal);
                break;
            case INDEX_OVERWRITE_AIR:
                _toolService.QueueAction(RegionToolAction.ToggleOverwriteAir);
                break;
            case INDEX_BLUEPRINTS:
                _blueprintLibrary.Value.Open();
                break;
        }
    }

    private bool IsNodeSelected(int index)
    {
        if (index == _hoveredIndex)
        {
            return true;
        }

        return index switch
        {
            INDEX_SELECT => _toolState.Mode == RegionToolMode.Select,
            INDEX_PASTE => _toolState.Mode == RegionToolMode.Paste,
            INDEX_OVERWRITE_AIR => _toolState.OverwriteAir,
            _ => false,
        };
    }

    private bool IsHoldingProjector()
    {
        if (!WaywardBeyond.IsPlaying())
        {
            return false;
        }

        Result<ItemSlot> mainHand = _playerData.GetMainHand(_ecsContext.World.DataStore);
        return mainHand.Success && mainHand.Value.Item.Tool?.Type == ToolType.Blueprint;
    }
}
