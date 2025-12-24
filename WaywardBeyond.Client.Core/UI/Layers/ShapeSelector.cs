using System;
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
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Items;
using WaywardBeyond.Client.Core.Player;
using WaywardBeyond.Client.Core.Systems;

namespace WaywardBeyond.Client.Core.UI.Layers;

using ShapeSelectorElement = (string ID, Material BaseImage, Material SelectedImage);

internal class ShapeSelector : IUILayer
{
    public bool Available => IsMainHandShapeable();

    private readonly PlayerControllerSystem _playerControllerSystem;
    private readonly PlayerInteractionService _playerInteractionService;
    private readonly PlayerData _playerData;
    private readonly BrickDatabase _brickDatabase;
    private readonly IECSContext _ecsContext;
    private readonly ILocalization _localization;
    
    private readonly Material _labelImage;
    private readonly Material _backgroundImage;
    private readonly Dictionary<BrickShape, ShapeSelectorElement> _shapeSelectorElements;
    
    private bool _changingShape;
    private bool _previousMouseLookState;
    private PlayerInteractionService.InteractionBlocker? _interactionBlocker;
    
    public ShapeSelector(
        IShortcutService shortcutService,
        IAssetDatabase<Texture> textureDatabase,
        IAssetDatabase<Shader> shaderDatabase,
        PlayerControllerSystem playerControllerSystem,
        PlayerInteractionService playerInteractionService,
        PlayerData playerData,
        BrickDatabase brickDatabase,
        IECSContext ecsContext,
        in ILocalization localization
    ) {
        _playerControllerSystem = playerControllerSystem;
        _playerInteractionService = playerInteractionService;
        _playerData = playerData;
        _brickDatabase = brickDatabase;
        _ecsContext = ecsContext;
        _localization = localization;
        
        Result<Shader> shader = shaderDatabase.Get("ui_reef_textured");
        _backgroundImage = new Material(shader, textureDatabase.Get("ui/shape_background.png"));
        _labelImage = new Material(shader, textureDatabase.Get("ui/shape_label.png"));
        
        _shapeSelectorElements = new Dictionary<BrickShape, ShapeSelectorElement>
        {
            [BrickShape.Block] = new("shapeSelector1", new Material(shader, textureDatabase.Get("ui/block.png")), new Material(shader, textureDatabase.Get("ui/shape_selected_block.png"))),
            [BrickShape.Slab]  = new("shapeSelector2", new Material(shader, textureDatabase.Get("ui/slab.png")), new Material(shader, textureDatabase.Get("ui/shape_selected_slab.png"))),
            [BrickShape.Stair] = new("shapeSelector3", new Material(shader, textureDatabase.Get("ui/stair.png")), new Material(shader, textureDatabase.Get("ui/shape_selected_stair.png"))),
            [BrickShape.Slope] = new("shapeSelector4", new Material(shader, textureDatabase.Get("ui/slope.png")), new Material(shader, textureDatabase.Get("ui/shape_selected_slope.png"))),
            [BrickShape.Column] = new("shapeSelector5", new Material(shader, textureDatabase.Get("ui/column.png")), new Material(shader, textureDatabase.Get("ui/shape_selected_column.png"))),
            [BrickShape.Plate] = new("shapeSelector6", new Material(shader, textureDatabase.Get("ui/plate.png")), new Material(shader, textureDatabase.Get("ui/shape_selected_plate.png"))),
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
    
    public bool IsVisible()
    {
        //  Don't draw anything if the player isn't holding a shapeable item.
        return WaywardBeyond.GameState == GameState.Playing && IsMainHandShapeable();
    }

    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        BrickShape selectedShape = _playerInteractionService.SelectedShape.Get();
        
        //  Draw the currently selected shape
        ShapeSelectorElement selectedShapeElement = _shapeSelectorElements[selectedShape];
        using (ui.Image(selectedShapeElement.BaseImage))
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Bottom,
                X = new Relative(0.5f),
                Y = new Fixed(ui.Height - 100),
                Width = new Fixed(32),
                Height = new Fixed(32),
            };
        }
        
        //  Only display the selector if changing shapes
        if (!_changingShape)
        {
            return Result.FromSuccess();
        }
        
        const float elementOffset = 96;
        const float angleBetweenElements = 360f / (BrickShape.Custom - BrickShape.Block) * MathS.DEGREES_TO_RADIANS;

        //  Draw the selectors and handle changing the selected shape
        var updatedSelection = false;
        for (var shape = BrickShape.Block; shape < BrickShape.Custom; shape++)
        {
            ShapeSelectorElement shapeSelectorElement = _shapeSelectorElements[shape];
            
            //  Create the selectable rect
            using (ui.Element())
            {
                var vector = new Vector2(0f, elementOffset);
                var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angleBetweenElements * (int)shape);
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
                
                bool isSelected = _playerInteractionService.SelectedShape.Get() == shape;
                if (!updatedSelection && ui.Hovering())
                {
                    _playerInteractionService.SelectedShape.Set(shape);
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
        
        //  Draw a label indicating the currently selected shape's name
        using (ui.Image(_labelImage))
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                X = new Relative(0.5f),
                Y = new Relative(0.5f),
                Width = new Fixed(64),
                Height = new Fixed(24),
            };

            string shapeTranslationKey = GetShapeTranslationKey(_playerInteractionService.SelectedShape);
            using (ui.Text(_localization.GetString(shapeTranslationKey)!))
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                    X = new Relative(0.5f),
                    Y = new Relative(0.5f),
                };
            }
        }

        return Result.FromSuccess();
    }
    
    private void OnChangeShapePressed()
    {
        if (!IsMainHandShapeable() || !_playerInteractionService.TryBlockInteractionExclusive(out _interactionBlocker))
        {
            return;
        }
        
        _changingShape = true;
        _previousMouseLookState = _playerControllerSystem.IsMouseLookEnabled();
        _playerControllerSystem.SetMouseLook(false);
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
        _playerControllerSystem.SetMouseLook(_previousMouseLookState);
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
        Result<BrickInfo> brickInfoResult = _brickDatabase.Get(placeable.ID);
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