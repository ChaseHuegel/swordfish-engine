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
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Shortcuts;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Items;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Renders the radial color-swatch picker when holding R with the paint sprayer.</summary>
internal sealed class PaintColorSelector : IUILayer, IActionIndicator
{
    private readonly RadialMenuView _view;
    private readonly InteractionState _interactionState;
    private readonly PlayerData _playerData;
    private readonly IPaintDatabase _paintDatabase;
    private readonly IECSContext _ecsContext;
    private readonly Dictionary<string, Material> _swatchMaterials = new(StringComparer.Ordinal);
    private readonly Material _fallbackMaterial;

    private bool _selecting;
    private InteractionState.InteractionBlocker? _interactionBlocker;
    private List<Paint> _nodePaints = [];

    /// <summary>Gets whether the currently held item is the paint sprayer and color paints exist.</summary>
    public bool Available => IsHoldingSprayer() && _paintDatabase.ColorPaints.Count > 0;

    /// <summary>Initializes a new instance of the <see cref="PaintColorSelector"/> class.</summary>
    public PaintColorSelector(
        in InteractionState interactionState,
        in IShortcutService shortcutService,
        in IAssetDatabase<Shader> shaderDatabase,
        in PlayerData playerData,
        in IPaintDatabase paintDatabase,
        in IECSContext ecsContext,
        in ILocalization localization
    ) {
        _interactionState = interactionState;
        _playerData = playerData;
        _paintDatabase = paintDatabase;
        _ecsContext = ecsContext;
        _view = new RadialMenuView(localization);

        Result<Shader> shader = shaderDatabase.Get("ui_reef_textured");
        _fallbackMaterial = CreateSwatchMaterial(shader, "paint_swatch_default", Vector3.One);

        IReadOnlyList<Paint> colors = paintDatabase.ColorPaints;
        for (var i = 0; i < colors.Count; i++)
        {
            Paint paint = colors[i];
            _swatchMaterials[paint.ID] = CreateSwatchMaterial(shader, $"paint_swatch_{paint.ID}", paint.Color);
        }

        if (colors.Count > 0 && string.IsNullOrEmpty(_interactionState.SelectedColorPaintId.Get()))
        {
            _interactionState.SelectedColorPaintId.Set(colors[0].ID);
        }

        var shortcut = new Shortcut
        {
            Name = "Select paint color",
            Category = "Interaction",
            Modifiers = ShortcutModifiers.None,
            Key = Key.R,
            SharedBindingGroup = GameShortcuts.TOOL_RADIAL_GROUP,
            IsEnabled = Shortcut.DefaultEnabled,
            Action = OnPressed,
            Released = OnReleased,
        };

        shortcutService.RegisterShortcut(shortcut);
    }

    /// <inheritdoc/>
    bool IUILayer.IsVisible() => _selecting;

    /// <inheritdoc/>
    bool IActionIndicator.IsVisible() => Available;

    /// <inheritdoc/>
    Material? IActionIndicator.IndicatorIcon
    {
        get
        {
            string? selectedId = _interactionState.SelectedColorPaintId.Get();
            return selectedId != null && _swatchMaterials.TryGetValue(selectedId, out Material? material)
                ? material
                : _fallbackMaterial;
        }
    }

    /// <inheritdoc/>
    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        IReadOnlyList<Paint> colors = _paintDatabase.ColorPaints;
        if (colors.Count == 0)
        {
            return Result.FromSuccess();
        }

        RadialMenuLayoutResult layout = RadialMenuLayout.Compute(colors.Count, ui.Width, ui.Height);
        string? selectedId = _interactionState.SelectedColorPaintId.Get();
        var nodes = new List<RadialNodeState>(colors.Count);
        var paints = new List<Paint>(colors.Count);
        string selectedLabel = colors[0].Name;

        for (var i = 0; i < colors.Count; i++)
        {
            Paint paint = colors[i];
            Vector2 offset = layout.Positions[i];
            bool isSelected = string.Equals(selectedId, paint.ID, StringComparison.Ordinal);
            if (isSelected)
            {
                selectedLabel = paint.Name;
            }

            Material icon = _swatchMaterials.TryGetValue(paint.ID, out Material? material) ? material : _fallbackMaterial;
            paints.Add(paint);
            nodes.Add(new RadialNodeState(
                $"paintColorSelector{i + 1}",
                i,
                (int)offset.X,
                (int)offset.Y,
                icon,
                isSelected
            ));
        }

        _nodePaints = paints;
        _view.Render(ui, new RadialMenuState(selectedLabel, nodes, OnNodeEnter, layout.HitSize));
        return Result.FromSuccess();
    }

    private void OnNodeEnter(int index)
    {
        if (index < 0 || index >= _nodePaints.Count)
        {
            return;
        }

        _interactionState.SelectColorPaint(_nodePaints[index].ID);
    }

    private void OnPressed()
    {
        if (!Available || !_interactionState.TryBlockInteractionExclusive(out _interactionBlocker))
        {
            return;
        }

        _selecting = true;
    }

    private void OnReleased()
    {
        if (!_selecting)
        {
            return;
        }

        _interactionBlocker?.Dispose();
        _interactionBlocker = null;
        _selecting = false;
    }

    private bool IsHoldingSprayer()
    {
        Result<ItemSlot> mainHand = _playerData.GetMainHand(_ecsContext.World.DataStore);
        return mainHand.Success && mainHand.Value.Item.Tool?.Type == ToolType.Sprayer;
    }

    private static Material CreateSwatchMaterial(Result<Shader> shader, string name, Vector3 color)
    {
        var r = (byte)Math.Clamp((int)MathF.Round(color.X * 255f), 0, 255);
        var g = (byte)Math.Clamp((int)MathF.Round(color.Y * 255f), 0, 255);
        var b = (byte)Math.Clamp((int)MathF.Round(color.Z * 255f), 0, 255);
        var texture = new Texture(name, [r, g, b, 255], 1, 1, mipmaps: false);
        return new Material(shader, texture);
    }
}
