using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Shoal.DependencyInjection;
using Swordfish.Bricks;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.UI.Reef;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Items;
using WaywardBeyond.Client.Core.Player;
using WaywardBeyond.Client.Core.Systems;

namespace WaywardBeyond.Client.Core.UI;

using OrientationSelectorElement = (string ID, Material BaseImage, Material SelectedImage);

internal class OrientationSelector : IAutoActivate
{
    public readonly DataBinding<BrickOrientation> SelectedOrientation = new();
    public bool Available => IsMainHandOrientable();
    
    private readonly ILogger _logger;
    private readonly ReefContext _reefContext;
    private readonly PlayerControllerSystem _playerControllerSystem;
    private readonly PlayerData _playerData;
    private readonly BrickDatabase _brickDatabase;
    private readonly IECSContext _ecsContext;
    private readonly ShapeSelector _shapeSelector;
    
    private readonly Material _backgroundImage;
    private readonly Dictionary<BrickOrientation, OrientationSelectorElement> _orientationSelectorElements;

    private readonly BrickOrientation _orientRight = new(pitch: 0, yaw: 0, roll: 1);
    private readonly BrickOrientation _orientDown = new(pitch: 0, yaw: 0, roll: 2);
    private readonly BrickOrientation _orientLeft = new(pitch: 0, yaw: 0, roll: 3);
    private readonly BrickOrientation _orientUp = new(pitch: 0, yaw: 0, roll: 0);
    private readonly BrickOrientation[] _orientations;
    
    private bool _changingOrientation;
    private bool _previousMouseLookState;
    
    public OrientationSelector(
        ILogger<OrientationSelector> logger,
        IWindowContext windowContext,
        ReefContext reefContext,
        IShortcutService shortcutService,
        IAssetDatabase<Texture> textureDatabase,
        IAssetDatabase<Shader> shaderDatabase,
        PlayerControllerSystem playerControllerSystem,
        PlayerData playerData,
        BrickDatabase brickDatabase,
        IECSContext ecsContext,
        ShapeSelector shapeSelector
    ) {
        _logger = logger;
        _reefContext = reefContext;
        _playerControllerSystem = playerControllerSystem;
        _playerData = playerData;
        _brickDatabase = brickDatabase;
        _ecsContext = ecsContext;
        _shapeSelector = shapeSelector;
        
        Result<Shader> shader = shaderDatabase.Get("ui_reef_textured");
        _backgroundImage = new Material(shader, textureDatabase.Get("ui/shape_background.png"));

        _orientations = [
            _orientDown,
            _orientLeft,
            _orientUp,
            _orientRight,
        ];
        
        _orientationSelectorElements = new Dictionary<BrickOrientation, OrientationSelectorElement>
        {
            [_orientDown]  = new("orientationSelector1", new Material(shader, textureDatabase.Get("ui/face_down.png")), new Material(shader, textureDatabase.Get("ui/face_down_selected.png"))),
            [_orientLeft] = new("orientationSelector2", new Material(shader, textureDatabase.Get("ui/face_left.png")), new Material(shader, textureDatabase.Get("ui/face_left_selected.png"))),
            [_orientUp] = new("orientationSelector3", new Material(shader, textureDatabase.Get("ui/face_up.png")), new Material(shader, textureDatabase.Get("ui/face_up_selected.png"))),
            [_orientRight] = new("orientationSelector4", new Material(shader, textureDatabase.Get("ui/face_right.png")), new Material(shader, textureDatabase.Get("ui/face_right_selected.png"))),
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
        
        windowContext.Update += OnWindowUpdate;
    }

    private void OnChangeOrientationPressed()
    {
        if (!IsMainHandOrientable())
        {
            return;
        }
        
        _changingOrientation = true;
        _previousMouseLookState = _playerControllerSystem.IsMouseLookEnabled();
        _playerControllerSystem.SetMouseLook(false);
    }
    
    private void OnChangeOrientationReleased()
    {
        if (!IsMainHandOrientable())
        {
            return;
        }
        
        _changingOrientation = false;
        _playerControllerSystem.SetMouseLook(_previousMouseLookState);
    }
    
    private bool IsMainHandOrientable() 
    {
        Result<ItemSlot> mainHandResult = _playerData.GetMainHand(_ecsContext.World.DataStore);
        if (!mainHandResult.Success)
        {
            _logger.LogError(mainHandResult.Exception, "Failed to get the player's main hand. {message}", mainHandResult.Message);
            return false;
        }

        if (mainHandResult.Value.Item.Placeable == null)
        {
            return false;
        }
        
        PlaceableDefinition placeable = mainHandResult.Value.Item.Placeable.Value;
        Result<BrickInfo> brickInfoResult = _brickDatabase.Get(placeable.ID);
        if (!brickInfoResult.Success)
        {
            return false;
        }

        BrickInfo brickInfo = brickInfoResult.Value;
        BrickShape brickShape = _shapeSelector.SelectedShape.Get();
        return brickInfo.IsOrientable(brickShape);
    }

    private void OnWindowUpdate(double delta)
    {
        if (!IsMainHandOrientable())
        {
            //  Don't draw anything if the player isn't holding a shapeable item.
            return;
        }
        
        UIBuilder<Material> ui = _reefContext.Builder;
        
        //  Draw the currently selected orientation
        OrientationSelectorElement selectedShapeElement = _orientationSelectorElements[SelectedOrientation.Get()];
        using (ui.Image(selectedShapeElement.BaseImage))
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Bottom,
                X = new Relative(0.5f),
                Y = new Fixed(ui.Height - 132),
                Width = new Fixed(32),
                Height = new Fixed(32),
            };
        }
        
        //  Only display the selector if changing shapes
        if (!_changingOrientation)
        {
            return;
        }
        
        const float elementOffset = 72;
        float angleBetweenElements = 360f / _orientations.Length * MathS.DEGREES_TO_RADIANS;

        //  Draw the selectors and handle changing the selected shape
        var updatedSelection = false;
        for (var i = 0; i < _orientations.Length; i++)
        {
            BrickOrientation orientation = _orientations[i];
            OrientationSelectorElement shapeSelectorElement = _orientationSelectorElements[orientation];
            
            //  Create the selectable rect
            using (ui.Element())
            {
                var vector = new Vector2(0f, elementOffset);
                var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angleBetweenElements * i);
                vector = Vector2.Transform(vector, rotation);
                
                ui.ID = shapeSelectorElement.ID;
                ui.Color = new Vector4(0f, 0f, 0f, 0f);
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                    X = new Fixed((int)vector.X + ui.Width / 2),
                    Y = new Fixed((int)vector.Y + ui.Height / 2),
                    Width = new Fixed(96),
                    Height = new Fixed(96),
                };
                
                bool isSelected = SelectedOrientation.Get().Equals(orientation);
                if (!updatedSelection && ui.Hovering())
                {
                    SelectedOrientation.Set(orientation);
                    updatedSelection = true;
                    isSelected = true;
                }

                //  Draw the selector
                using (ui.Image(_backgroundImage))
                {
                    ui.LayoutDirection = LayoutDirection.None;
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                        X = new Relative(0.5f),
                        Y = new Relative(0.5f),
                        Width = new Fixed(64),
                        Height = new Fixed(64),
                    };

                    //  Draw the selection indicator
                    if (isSelected)
                    {
                        using (ui.Image(shapeSelectorElement.SelectedImage))
                        {
                            ui.Constraints = new Constraints
                            {
                                Width = new Fixed(64),
                                Height = new Fixed(64),
                            };
                        }
                    }

                    //  Draw the shape
                    using (ui.Image(shapeSelectorElement.BaseImage))
                    {
                        ui.Constraints = new Constraints
                        {
                            Anchors = Anchors.Center,
                            X = new Relative(0.5f),
                            Y = new Relative(0.5f),
                            Width = new Fixed(32),
                            Height = new Fixed(32),
                        };
                    }
                }
            }
        }
    }
}