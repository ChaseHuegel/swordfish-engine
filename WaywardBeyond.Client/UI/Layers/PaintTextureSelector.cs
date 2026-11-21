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
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Items;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Renders the radial texture-paint picker when holding R with the drill.</summary>
internal sealed class PaintTextureSelector : IUILayer, IActionIndicator
{
    private readonly RadialMenuView _view;
    private readonly InteractionState _interactionState;
    private readonly PlayerData _playerData;
    private readonly IPaintDatabase _paintDatabase;
    private readonly IECSContext _ecsContext;
    private readonly Dictionary<string, Material> _textureMaterials = new(StringComparer.Ordinal);
    private readonly Material _fallbackMaterial;

    private bool _selecting;
    private InteractionState.InteractionBlocker? _interactionBlocker;
    private List<Paint> _nodePaints = [];

    /// <summary>Gets whether the currently held item is the drill and texture paints exist.</summary>
    public bool Available => IsHoldingDrill() && _paintDatabase.TexturePaints.Count > 0;

    /// <summary>Initializes a new instance of the <see cref="PaintTextureSelector"/> class.</summary>
    public PaintTextureSelector(
        in InteractionState interactionState,
        in IShortcutService shortcutService,
        in IAssetDatabase<Texture> textureDatabase,
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
        var fallbackTexture = new Texture("paint_texture_fallback", [200, 200, 200, 255], 1, 1, mipmaps: false);
        _fallbackMaterial = new Material(shader, fallbackTexture);

        IReadOnlyList<Paint> textures = paintDatabase.TexturePaints;
        for (var i = 0; i < textures.Count; i++)
        {
            Paint paint = textures[i];
            string? firstTex = paint.Textures is { Length: > 0 } ? paint.Textures[0] : null;
            Result<Texture> texResult = firstTex != null
                ? textureDatabase.Get($"block/{firstTex}.png")
                : Result<Texture>.FromFailure("No texture");
            _textureMaterials[paint.ID] = texResult.Success
                ? new Material(shader, texResult)
                : _fallbackMaterial;
        }

        if (textures.Count > 0 && string.IsNullOrEmpty(_interactionState.SelectedTexturePaintId.Get()))
        {
            _interactionState.SelectedTexturePaintId.Set(textures[0].ID);
        }

        var shortcut = new Shortcut
        {
            Name = "Select paint texture",
            Category = "Interaction",
            Modifiers = ShortcutModifiers.None,
            Key = Key.R,
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
            string? selectedId = _interactionState.SelectedTexturePaintId.Get();
            return selectedId != null && _textureMaterials.TryGetValue(selectedId, out Material? material)
                ? material
                : _fallbackMaterial;
        }
    }

    /// <inheritdoc/>
    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        IReadOnlyList<Paint> textures = _paintDatabase.TexturePaints;
        if (textures.Count == 0)
        {
            return Result.FromSuccess();
        }

        RadialMenuLayoutResult layout = RadialMenuLayout.Compute(textures.Count, ui.Width, ui.Height);
        string? selectedId = _interactionState.SelectedTexturePaintId.Get();
        var nodes = new List<RadialNodeState>(textures.Count);
        var paints = new List<Paint>(textures.Count);
        string selectedLabel = textures[0].Name;

        for (var i = 0; i < textures.Count; i++)
        {
            Paint paint = textures[i];
            Vector2 offset = layout.Positions[i];
            bool isSelected = string.Equals(selectedId, paint.ID, StringComparison.Ordinal);
            if (isSelected)
            {
                selectedLabel = paint.Name;
            }

            Material icon = _textureMaterials.TryGetValue(paint.ID, out Material? material) ? material : _fallbackMaterial;
            paints.Add(paint);
            nodes.Add(new RadialNodeState(
                $"paintTextureSelector{i + 1}",
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

        _interactionState.SelectTexturePaint(_nodePaints[index].ID);
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

    private bool IsHoldingDrill()
    {
        Result<ItemSlot> mainHand = _playerData.GetMainHand(_ecsContext.World.DataStore);
        return mainHand.Success && mainHand.Value.Item.Tool?.Type == ToolType.Drill;
    }
}
