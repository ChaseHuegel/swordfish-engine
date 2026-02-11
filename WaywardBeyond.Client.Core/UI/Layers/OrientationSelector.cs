using System.Collections.Generic;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Items;
using WaywardBeyond.Client.Core.Player;
using WaywardBeyond.Client.Core.Systems;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.UI.Layers;

using OrientationSelectorElement = (string ID, Material BaseImage, Material SelectedImage);

internal class OrientationSelector : IUILayer
{
    public bool Available => IsMainHandOrientable();
    
    private readonly PlayerInteractionService _playerInteractionService;
    private readonly PlayerData _playerData;
    private readonly BrickDatabase _brickDatabase;
    private readonly IECSContext _ecsContext;
    
    private readonly Material _backgroundImage;
    private readonly Dictionary<Orientation, OrientationSelectorElement> _orientationSelectorElements;

    private readonly Orientation _orientRight = new(pitch: 0, yaw: 0, roll: 1);
    private readonly Orientation _orientDown = new(pitch: 0, yaw: 0, roll: 2);
    private readonly Orientation _orientLeft = new(pitch: 0, yaw: 0, roll: 3);
    private readonly Orientation _orientUp = new(pitch: 0, yaw: 0, roll: 0);
    private readonly Orientation[] _orientations;
    
    private bool _changingOrientation;
    private PlayerInteractionService.InteractionBlocker? _interactionBlocker;
    
    public OrientationSelector(
        IShortcutService shortcutService,
        IAssetDatabase<Texture> textureDatabase,
        IAssetDatabase<Shader> shaderDatabase,
        PlayerInteractionService playerInteractionService,
        PlayerData playerData,
        BrickDatabase brickDatabase,
        IECSContext ecsContext
    ) {
        _playerInteractionService = playerInteractionService;
        _playerData = playerData;
        _brickDatabase = brickDatabase;
        _ecsContext = ecsContext;
        
        Result<Shader> shader = shaderDatabase.Get("ui_reef_textured");
        _backgroundImage = new Material(shader, textureDatabase.Get("ui/shape_background.png"));

        _orientations = [
            _orientDown,
            _orientLeft,
            _orientUp,
            _orientRight,
        ];
        
        _orientationSelectorElements = new Dictionary<Orientation, OrientationSelectorElement>
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
    }
    
    public bool IsVisible()
    {
        //  Don't draw anything if the player isn't holding a shapeable item.
        return WaywardBeyond.GameState == GameState.Playing && IsMainHandOrientable();
    }

    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        //  Draw the currently selected orientation
        OrientationSelectorElement selectedShapeElement = _orientationSelectorElements[_playerInteractionService.SelectedOrientation.Get()];
        using (ui.Image(selectedShapeElement.BaseImage))
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Bottom,
                Y = new Fixed(-138),
                Width = new Fixed(32),
                Height = new Fixed(32),
            };
        }
        
        //  Only display the selector if changing shapes
        if (!_changingOrientation)
        {
            return Result.FromSuccess();
        }
        
        const float elementOffset = 72;
        float angleBetweenElements = 360f / _orientations.Length * MathS.DEGREES_TO_RADIANS;

        //  Draw the selectors and handle changing the selected shape
        var updatedSelection = false;
        for (var i = 0; i < _orientations.Length; i++)
        {
            Orientation orientation = _orientations[i];
            OrientationSelectorElement orientationSelectorElement = _orientationSelectorElements[orientation];
            
            //  Create the selectable rect
            using (ui.Element(orientationSelectorElement.ID))
            {
                var vector = new Vector2(0f, elementOffset);
                var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angleBetweenElements * i);
                vector = Vector2.Transform(vector, rotation);

                ui.LayoutDirection = LayoutDirection.None;
                ui.Color = new Vector4(0f, 0f, 0f, 0f);
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                    X = new Fixed((int)vector.X),
                    Y = new Fixed((int)vector.Y),
                    Width = new Fixed(96),
                    Height = new Fixed(96),
                };
                
                bool isSelected = _playerInteractionService.SelectedOrientation.Get().Equals(orientation);
                if (!updatedSelection && ui.Hovering())
                {
                    _playerInteractionService.SelectedOrientation.Set(orientation);
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
                        Width = new Fixed(64),
                        Height = new Fixed(64),
                    };

                    //  Draw the selection indicator
                    if (isSelected)
                    {
                        using (ui.Image(orientationSelectorElement.SelectedImage))
                        {
                            ui.Constraints = new Constraints
                            {
                                Width = new Fixed(64),
                                Height = new Fixed(64),
                            };
                        }
                    }

                    //  Draw the shape
                    using (ui.Image(orientationSelectorElement.BaseImage))
                    {
                        ui.Constraints = new Constraints
                        {
                            Anchors = Anchors.Center,
                            Width = new Fixed(32),
                            Height = new Fixed(32),
                        };
                    }
                }
            }
        }
        
        return Result.FromSuccess();
    }
    
    private void OnChangeOrientationPressed()
    {
        if (!IsMainHandOrientable() || !_playerInteractionService.TryBlockInteractionExclusive(out _interactionBlocker))
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
        Result<BrickInfo> brickInfoResult = _brickDatabase.Get(placeable.ID);
        if (!brickInfoResult.Success)
        {
            return false;
        }

        BrickInfo brickInfo = brickInfoResult.Value;
        BrickShape brickShape = _playerInteractionService.SelectedShape.Get();
        return brickInfo.IsOrientable(brickShape);
    }
}