using System.Collections.Generic;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Items;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Items;

namespace WaywardBeyond.Client.UI.Layers;

using ShapeSelectorElement = (string ID, Material Icon);

/// <summary>Renders the brick shape radial selector and its HUD indicator icon.</summary>
internal sealed class ShapeSelector : IUILayer, IActionIndicator
{
    private const int INDICATOR_ICON_SIZE = 32;
    private const int ICON_SIZE = 28;
    private const int RADIAL_NODE_SIZE = 56;
    private const int RADIAL_HITBOX_SIZE = 88;
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
    public Result RenderIndicator(double delta, UIBuilder<Material> ui)
    {
        BrickShape shape = _interactionState.SelectedShape.Get();
        ShapeSelectorElement selectedShapeElement = _shapeSelectorElements[shape];

        using (ui.Image(selectedShapeElement.Icon))
        {
            ui.Passthrough = true;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(INDICATOR_ICON_SIZE),
                Height = new Fixed(INDICATOR_ICON_SIZE),
            };
        }

        return Result.FromSuccess();
    }

    /// <inheritdoc/>
    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        float angleBetweenElements = 360f / _shapeSelectorElements.Count * MathS.DEGREES_TO_RADIANS;

        var updatedSelection = false;
        var index = 0;
        for (var shape = BrickShape.Block; shape < BrickShape.Custom; shape++)
        {
            if (!_shapeSelectorElements.TryGetValue(shape, out ShapeSelectorElement shapeSelectorElement))
            {
                continue;
            }

            using (ui.Element(shapeSelectorElement.ID))
            {
                Vector2 offset = GetRadialOffset(angleBetweenElements, index);
                index++;

                DrawNodeHitbox(ui, offset);

                bool isSelected = _interactionState.SelectedShape.Get() == shape;
                if (!updatedSelection && ui.Hovering())
                {
                    _interactionState.SelectedShape.Set(shape);
                    updatedSelection = true;
                    isSelected = true;
                }

                DrawNode(ui, shapeSelectorElement.Icon, isSelected);
            }
        }

        DrawSelectedShapeLabel(ui);

        return Result.FromSuccess();
    }

    /// <summary>Computes the position of a radial node on the selection circle.</summary>
    private static Vector2 GetRadialOffset(float angleBetweenElements, int index)
    {
        var offset = new Vector2(0f, ELEMENT_OFFSET);
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angleBetweenElements * index);
        return Vector2.Transform(offset, rotation);
    }

    /// <summary>Draws the hitbox of one radial node and places it at the given offset.</summary>
    private static void DrawNodeHitbox(UIBuilder<Material> ui, Vector2 offset)
    {
        ui.LayoutDirection = LayoutDirection.None;
        ui.Color = UiTheme.Transparent;
        ui.Constraints = new Constraints
        {
            Anchors = Anchors.Center,
            X = new Fixed((int)offset.X),
            Y = new Fixed((int)offset.Y),
            Width = new Fixed(RADIAL_HITBOX_SIZE),
            Height = new Fixed(RADIAL_HITBOX_SIZE),
        };
    }

    /// <summary>Draws the node plate and icon of one shape.</summary>
    private static void DrawNode(UIBuilder<Material> ui, Material icon, bool isSelected)
    {
        using (ui.Element())
        {
            ui.Passthrough = true;
            ui.LayoutDirection = LayoutDirection.None;
            ApplyNodeStyle(ui, isSelected);
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(RADIAL_NODE_SIZE),
                Height = new Fixed(RADIAL_NODE_SIZE),
            };

            using (ui.Image(icon))
            {
                ui.Passthrough = true;
                ui.Color = isSelected ? UiTheme.PrimaryBright : UiTheme.AccentBright;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                    Width = new Fixed(ICON_SIZE),
                    Height = new Fixed(ICON_SIZE),
                };
            }
        }
    }

    /// <summary>Applies the fill, border, and corner caps of a radial node plate.</summary>
    private static void ApplyNodeStyle(UIBuilder<Material> ui, bool selected)
    {
        ui.Color = selected ? UiTheme.SelectionBackground : UiTheme.PanelBackground;
        ui.Border = new BorderStyle
        {
            Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
            Color = selected ? UiTheme.Primary : UiTheme.Border,
        };
        ui.CornerCaps = new CornerCapStyle
        {
            Length = UiMetrics.SELECTION_CAP_LENGTH,
            Thickness = UiMetrics.SELECTION_CAP_THICKNESS,
            Color = selected ? UiTheme.PrimaryBright : UiTheme.BorderHover,
        };
    }

    /// <summary>Draws the centered label of the selected shape.</summary>
    private void DrawSelectedShapeLabel(UIBuilder<Material> ui)
    {
        string shapeTranslationKey = GetShapeTranslationKey(_interactionState.SelectedShape);
        string shapeLabel = _localization.GetString(shapeTranslationKey) ?? shapeTranslationKey;

        using (ui.Element())
        {
            ui.Passthrough = true;
            ui.LayoutDirection = LayoutDirection.None;
            ui.Padding = new Padding(left: 14, top: 6, right: 14, bottom: 6);
            ui.Color = UiTheme.PanelBackground;
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
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };

            using (ui.Text(shapeLabel, UiMetrics.SMALL_FONT))
            {
                ui.Passthrough = true;
                ui.Color = UiTheme.PrimaryBright;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };
            }
        }
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
