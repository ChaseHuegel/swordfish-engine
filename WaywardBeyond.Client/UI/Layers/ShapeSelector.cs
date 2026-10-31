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
using WaywardBeyond.Items;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Systems;

namespace WaywardBeyond.Client.UI.Layers;

using ShapeSelectorElement = (string ID, Material Icon);

/// <summary>Renders the brick shape radial selector and its HUD indicator icon.</summary>
internal sealed class ShapeSelector : IUILayer, IActionIndicator
{
    private const float ELEMENT_OFFSET = 96f;

    /// <summary>Gets whether the currently held item supports shape selection.</summary>
    public bool Available => IsMainHandShapeable();

    private readonly InteractionState _interactionState;
    private readonly PlayerData _playerData;
    private readonly IBrickDatabase _brickDatabase;
    private readonly IECSContext _ecsContext;
    private readonly ILocalization _localization;

    private readonly Dictionary<BrickShape, ShapeSelectorElement> _shapeSelectorElements;

    private bool _changingShape;
    private InteractionState.InteractionBlocker? _interactionBlocker;
    private List<BrickShape> _nodeShapes = [];

    /// <summary>Initializes a new instance of the <see cref="ShapeSelector"/> class.</summary>
    public ShapeSelector(
        in InteractionState interactionState,
        in IShortcutService shortcutService,
        in IAssetDatabase<Texture> textureDatabase,
        in IAssetDatabase<Shader> shaderDatabase,
        in PlayerData playerData,
        in IBrickDatabase brickDatabase,
        in IECSContext ecsContext,
        in ILocalization localization
    )
    {
        _interactionState = interactionState;
        _playerData = playerData;
        _brickDatabase = brickDatabase;
        _ecsContext = ecsContext;
        _localization = localization;

        Result<Shader> shader = shaderDatabase.Get("ui_reef_textured");

        _shapeSelectorElements = new Dictionary<BrickShape, ShapeSelectorElement>
        {
            [BrickShape.Block] = new("shapeSelector1", new Material(shader, textureDatabase.Get("ui/block.png"))),
            [BrickShape.Slab] = new("shapeSelector2", new Material(shader, textureDatabase.Get("ui/slab.png"))),
            [BrickShape.Stair] = new("shapeSelector3", new Material(shader, textureDatabase.Get("ui/stair.png"))),
            [BrickShape.Slope] = new("shapeSelector4", new Material(shader, textureDatabase.Get("ui/slope.png"))),
            [BrickShape.Column] = new("shapeSelector5", new Material(shader, textureDatabase.Get("ui/column.png"))),
            [BrickShape.Plate] = new("shapeSelector6", new Material(shader, textureDatabase.Get("ui/plate.png"))),
        };

        var shortcut = new Shortcut
        {
            Name = "Change shape",
            Category = "Interaction",
            Modifiers = ShortcutModifiers.None,
            Key = Key.R,
            IsEnabled = Shortcut.DefaultEnabled,
            Action = OnChangeShapePressed,
            Released = OnChangeShapeReleased,
        };

        shortcutService.RegisterShortcut(shortcut);
    }

    /// <inheritdoc/>
    bool IUILayer.IsVisible() => _changingShape;

    /// <inheritdoc/>
    bool IActionIndicator.IsVisible() => IsMainHandShapeable();

    /// <inheritdoc/>
    Material? IActionIndicator.IndicatorIcon =>
        _shapeSelectorElements[_interactionState.SelectedShape.Get()].Icon;

    /// <inheritdoc/>
    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        float angleBetweenElements = 360f / _shapeSelectorElements.Count * MathS.DEGREES_TO_RADIANS;
        BrickShape selectedShape = _interactionState.SelectedShape.Get();
        var nodes = new List<RadialNodeState>(_shapeSelectorElements.Count);
        var shapes = new List<BrickShape>(_shapeSelectorElements.Count);
        var index = 0;

        for (var shape = BrickShape.Block; shape < BrickShape.Custom; shape++)
        {
            if (!_shapeSelectorElements.TryGetValue(shape, out ShapeSelectorElement element))
            {
                continue;
            }

            Vector2 offset = GetRadialOffset(angleBetweenElements, index);
            shapes.Add(shape);
            nodes.Add(new RadialNodeState(
                element.ID,
                index,
                (int)offset.X,
                (int)offset.Y,
                element.Icon,
                selectedShape == shape
            ));
            index++;
        }

        string translationKey = GetShapeTranslationKey(selectedShape);
        string label = _localization.GetString(translationKey) ?? translationKey;
        _nodeShapes = shapes;

        new RadialMenuView().Render(ui, new RadialMenuState(label, nodes, OnNodeEnter));

        return Result.FromSuccess();
    }

    /// <summary>Selects the shape of the hovered node.</summary>
    private void OnNodeEnter(int index)
    {
        if (index < 0 || index >= _nodeShapes.Count)
        {
            return;
        }

        _interactionState.SelectedShape.Set(_nodeShapes[index]);
    }

    /// <summary>Computes the position of a radial node on the selection circle.</summary>
    private static Vector2 GetRadialOffset(float angleBetweenElements, int index)
    {
        var offset = new Vector2(0f, ELEMENT_OFFSET);
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angleBetweenElements * index);
        return Vector2.Transform(offset, rotation);
    }

    private void OnChangeShapePressed()
    {
        if (!IsMainHandShapeable() || !_interactionState.TryBlockInteractionExclusive(out _interactionBlocker))
        {
            return;
        }

        _changingShape = true;
    }

    private void OnChangeShapeReleased()
    {
        if (!_changingShape)
        {
            return;
        }

        _interactionBlocker?.Dispose();
        _interactionBlocker = null;
        _changingShape = false;
    }

    private bool IsMainHandShapeable()
    {
        Result<ItemSlot> mainHandResult = _playerData.GetMainHand(_ecsContext.World.DataStore);
        if (!mainHandResult.Success)
        {
            return false;
        }

        if (mainHandResult.Value.Item.Placeable == null)
        {
            return false;
        }

        PlaceableDefinition placeable = mainHandResult.Value.Item.Placeable.Value;
        Result<Brick> brickInfoResult = _brickDatabase.Get(placeable.ID);
        return brickInfoResult.Success && brickInfoResult.Value.Shapeable;
    }

    private static string GetShapeTranslationKey(BrickShape shape)
    {
        return shape switch
        {
            BrickShape.Block => "brick.shape.block",
            BrickShape.Slab => "brick.shape.slab",
            BrickShape.Stair => "brick.shape.stair",
            BrickShape.Slope => "brick.shape.slope",
            BrickShape.Column => "brick.shape.column",
            BrickShape.Plate => "brick.shape.plate",
            _ => "UNKNOWN",
        };
    }
}
