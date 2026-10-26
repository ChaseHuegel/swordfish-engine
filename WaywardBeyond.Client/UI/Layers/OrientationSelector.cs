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
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.UI.Layers;

using OrientationSelectorElement = (string ID, string TranslationKey, Material Icon);

/// <summary>Renders the brick orientation radial selector and its HUD indicator icon.</summary>
internal sealed class OrientationSelector : IUILayer, IActionIndicator
{
    private const int INDICATOR_ICON_SIZE = 32;
    private const int ICON_SIZE = 28;
    private const int RADIAL_NODE_SIZE = 56;
    private const int RADIAL_HITBOX_SIZE = 88;
    private const float ELEMENT_OFFSET = 76f;

    /// <summary>Gets whether the currently held item and shape support orientation selection.</summary>
    public bool Available => IsMainHandOrientable();

    private readonly InteractionState _interactionState;
    private readonly PlayerData _playerData;
    private readonly IBrickDatabase _brickDatabase;
    private readonly IECSContext _ecsContext;
    private readonly ILocalization _localization;

    private readonly Dictionary<Orientation, OrientationSelectorElement> _orientationSelectorElements;

    private readonly Orientation _orientRight = new(pitch: 0, yaw: 0, roll: 1);
    private readonly Orientation _orientDown = new(pitch: 0, yaw: 0, roll: 2);
    private readonly Orientation _orientLeft = new(pitch: 0, yaw: 0, roll: 3);
    private readonly Orientation _orientUp = new(pitch: 0, yaw: 0, roll: 0);
    private readonly Orientation[] _orientations;

    private bool _changingOrientation;
    private InteractionState.InteractionBlocker? _interactionBlocker;

    /// <summary>Initializes a new instance of the <see cref="OrientationSelector"/> class.</summary>
    public OrientationSelector(
        in IShortcutService shortcutService,
        in IAssetDatabase<Texture> textureDatabase,
        in IAssetDatabase<Shader> shaderDatabase,
        in InteractionState interactionState,
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

        _orientations =
        [
            _orientDown,
            _orientLeft,
            _orientUp,
            _orientRight,
        ];

        _orientationSelectorElements = new Dictionary<Orientation, OrientationSelectorElement>
        {
            [_orientDown] = new("orientationSelector1", "brick.orientation.down", new Material(shader, textureDatabase.Get("ui/face_down.png"))),
            [_orientLeft] = new("orientationSelector2", "brick.orientation.left", new Material(shader, textureDatabase.Get("ui/face_left.png"))),
            [_orientUp] = new("orientationSelector3", "brick.orientation.up", new Material(shader, textureDatabase.Get("ui/face_up.png"))),
            [_orientRight] = new("orientationSelector4", "brick.orientation.right", new Material(shader, textureDatabase.Get("ui/face_right.png"))),
        };

        var shortcut = new Shortcut
        {
            Name = "Change orientation",
            Category = "Interaction",
            Modifiers = ShortcutModifiers.None,
            Key = Key.T,
            IsEnabled = Shortcut.DefaultEnabled,
            Action = OnChangeOrientationPressed,
            Released = OnChangeOrientationReleased,
        };

        shortcutService.RegisterShortcut(shortcut);
    }

    /// <inheritdoc/>
    bool IUILayer.IsVisible() => _changingOrientation;

    /// <inheritdoc/>
    bool IActionIndicator.IsVisible() => IsMainHandOrientable();

    /// <inheritdoc/>
    public Result RenderIndicator(double delta, UIBuilder<Material> ui)
    {
        Orientation orientation = _interactionState.SelectedOrientation.Get();
        OrientationSelectorElement selectedElement = _orientationSelectorElements[orientation];

        using (ui.Image(selectedElement.Icon))
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
        float angleBetweenElements = 360f / _orientations.Length * MathS.DEGREES_TO_RADIANS;

        var updatedSelection = false;
        for (var i = 0; i < _orientations.Length; i++)
        {
            Orientation orientation = _orientations[i];
            OrientationSelectorElement orientationSelectorElement = _orientationSelectorElements[orientation];

            using (ui.Element(orientationSelectorElement.ID))
            {
                var vector = new Vector2(0f, ELEMENT_OFFSET);
                var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angleBetweenElements * i);
                vector = Vector2.Transform(vector, rotation);

                ui.LayoutDirection = LayoutDirection.None;
                ui.Color = UiTheme.Transparent;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                    X = new Fixed((int)vector.X),
                    Y = new Fixed((int)vector.Y),
                    Width = new Fixed(RADIAL_HITBOX_SIZE),
                    Height = new Fixed(RADIAL_HITBOX_SIZE),
                };

                bool isSelected = _interactionState.SelectedOrientation.Get().Equals(orientation);
                if (!updatedSelection && ui.Hovering())
                {
                    _interactionState.SelectedOrientation.Set(orientation);
                    updatedSelection = true;
                    isSelected = true;
                }

                using (ui.Element())
                {
                    ui.Passthrough = true;
                    ui.LayoutDirection = LayoutDirection.None;
                    if (isSelected)
                    {
                        ui.Color = UiTheme.SelectionBackground;
                        ui.Border = new BorderStyle
                        {
                            Thickness = UiMetrics.PanelBorderThickness,
                            Color = UiTheme.Primary,
                        };
                        ui.CornerCaps = new CornerCapStyle
                        {
                            Length = UiMetrics.SelectionCapLength,
                            Thickness = UiMetrics.SelectionCapThickness,
                            Color = UiTheme.PrimaryBright,
                        };
                    }
                    else
                    {
                        ui.Color = UiTheme.PanelBackground;
                        ui.Border = new BorderStyle
                        {
                            Thickness = UiMetrics.PanelBorderThickness,
                            Color = UiTheme.Border,
                        };
                        ui.CornerCaps = new CornerCapStyle
                        {
                            Length = UiMetrics.SelectionCapLength,
                            Thickness = UiMetrics.SelectionCapThickness,
                            Color = UiTheme.BorderHover,
                        };
                    }

                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                        Width = new Fixed(RADIAL_NODE_SIZE),
                        Height = new Fixed(RADIAL_NODE_SIZE),
                    };

                    using (ui.Image(orientationSelectorElement.Icon))
                    {
                        ui.Passthrough = true;
                        if (isSelected)
                        {
                            ui.Color = UiTheme.PrimaryBright;
                        }
                        else
                        {
                            ui.Color = UiTheme.AccentBright;
                        }

                        ui.Constraints = new Constraints
                        {
                            Anchors = Anchors.Center,
                            Width = new Fixed(ICON_SIZE),
                            Height = new Fixed(ICON_SIZE),
                        };
                    }
                }
            }
        }

        Orientation activeOrientation = _interactionState.SelectedOrientation.Get();
        string translationKey = "brick.orientation.up";
        if (_orientationSelectorElements.TryGetValue(activeOrientation, out OrientationSelectorElement activeElement))
        {
            translationKey = activeElement.TranslationKey;
        }

        using (ui.Element())
        {
            ui.Passthrough = true;
            ui.LayoutDirection = LayoutDirection.None;
            ui.Padding = new Padding(left: 14, top: 6, right: 14, bottom: 6);
            ui.Color = UiTheme.PanelBackground;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PanelBorderThickness,
                Color = UiTheme.Primary,
            };
            ui.CornerCaps = new CornerCapStyle
            {
                Length = UiMetrics.SelectionCapLength,
                Thickness = UiMetrics.SelectionCapThickness,
                Color = UiTheme.PrimaryBright,
            };
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };

            using (ui.Text(_localization.GetString(translationKey) ?? translationKey, UiMetrics.SmallFont))
            {
                ui.Passthrough = true;
                ui.Color = UiTheme.PrimaryBright;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };
            }
        }

        return Result.FromSuccess();
    }
    
    private void OnChangeOrientationPressed()
    {
        if (!IsMainHandOrientable() || !_interactionState.TryBlockInteractionExclusive(out _interactionBlocker))
        {
            return;
        }
        
        _changingOrientation = true;
    }
    
    private void OnChangeOrientationReleased()
    {
        if (!_changingOrientation)
        {
            return;
        }
        
        _interactionBlocker?.Dispose();
        _interactionBlocker = null;
        _changingOrientation = false;
    }
    
    private bool IsMainHandOrientable() 
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
        if (!brickInfoResult.Success)
        {
            return false;
        }

        Brick brickInfo = brickInfoResult.Value;
        BrickShape brickShape = _interactionState.SelectedShape.Get();
        return brickInfo.IsOrientable(brickShape);
    }
}