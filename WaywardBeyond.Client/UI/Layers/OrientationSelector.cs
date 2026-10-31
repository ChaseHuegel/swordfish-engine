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
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.UI.Layers;

using OrientationSelectorElement = (string ID, string TranslationKey, Material Icon);

/// <summary>Renders the brick orientation radial selector and its HUD indicator icon.</summary>
internal sealed class OrientationSelector : IUILayer, IActionIndicator
{
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
    Material? IActionIndicator.IndicatorIcon =>
        _orientationSelectorElements[_interactionState.SelectedOrientation.Get()].Icon;

    /// <inheritdoc/>
    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        float angleBetweenElements = 360f / _orientations.Length * MathS.DEGREES_TO_RADIANS;
        Orientation selectedOrientation = _interactionState.SelectedOrientation.Get();
        var nodes = new List<RadialNodeState>(_orientations.Length);

        for (var i = 0; i < _orientations.Length; i++)
        {
            Orientation orientation = _orientations[i];
            OrientationSelectorElement element = _orientationSelectorElements[orientation];
            Vector2 offset = GetRadialOffset(angleBetweenElements, i);
            nodes.Add(new RadialNodeState(
                element.ID,
                i,
                (int)offset.X,
                (int)offset.Y,
                element.Icon,
                selectedOrientation.Equals(orientation)
            ));
        }

        string translationKey = GetSelectedOrientationKey();
        string label = _localization.GetString(translationKey) ?? translationKey;

        new RadialMenuView().Render(ui, new RadialMenuState(label, nodes, OnNodeEnter));

        return Result.FromSuccess();
    }

    /// <summary>Selects the orientation of the hovered node.</summary>
    private void OnNodeEnter(int index)
    {
        if (index < 0 || index >= _orientations.Length)
        {
            return;
        }

        _interactionState.SelectedOrientation.Set(_orientations[index]);
    }

    /// <summary>Computes the position of a radial node on the selection circle.</summary>
    private static Vector2 GetRadialOffset(float angleBetweenElements, int index)
    {
        var offset = new Vector2(0f, ELEMENT_OFFSET);
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angleBetweenElements * index);
        return Vector2.Transform(offset, rotation);
    }

    /// <summary>Resolves the translation key of the selected orientation.</summary>
    private string GetSelectedOrientationKey()
    {
        Orientation activeOrientation = _interactionState.SelectedOrientation.Get();
        string translationKey = "brick.orientation.up";
        if (_orientationSelectorElements.TryGetValue(activeOrientation, out OrientationSelectorElement activeElement))
        {
            translationKey = activeElement.TranslationKey;
        }

        return translationKey;
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
