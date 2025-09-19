using System.Collections.Generic;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.UI.Reef;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Systems;

namespace WaywardBeyond.Client.Core.UI;

using ShapeSelectorElement = (string ID, Material BaseImage, Material SelectedImage);

internal class ShapeSelector
{
    private readonly ReefContext _reefContext;
    private readonly PlayerControllerSystem _playerControllerSystem;

    public readonly DataBinding<BrickShape> SelectedShape = new(BrickShape.Block);
    
    private readonly Material _labelImage;
    private readonly Material _backgroundImage;
    private readonly Dictionary<BrickShape, ShapeSelectorElement> _shapeSelectorElements;
    
    private bool _changingShape;
    private bool _previousMouseLookState;
    
    public ShapeSelector(
        IWindowContext windowContext,
        ReefContext reefContext,
        IShortcutService shortcutService,
        IAssetDatabase<Texture> textureDatabase,
        IAssetDatabase<Shader> shaderDatabase,
        PlayerControllerSystem playerControllerSystem
    ) {
        _reefContext = reefContext;
        _playerControllerSystem = playerControllerSystem;
        
        Result<Shader> shader = shaderDatabase.Get("ui_reef_textured.glsl");
        _backgroundImage = new Material(shader, textureDatabase.Get("ui/shape_background.png"));
        _labelImage = new Material(shader, textureDatabase.Get("ui/shape_label.png"));
        
        _shapeSelectorElements = new Dictionary<BrickShape, ShapeSelectorElement>
        {
            [BrickShape.Block] = new("shapeSelector1", new Material(shader, textureDatabase.Get("ui/block.png")), new Material(shader, textureDatabase.Get("ui/shape_selected_block.png"))),
            [BrickShape.Slab]  = new("shapeSelector2", new Material(shader, textureDatabase.Get("ui/slab.png")), new Material(shader, textureDatabase.Get("ui/shape_selected_slab.png"))),
            [BrickShape.Stair] = new("shapeSelector3", new Material(shader, textureDatabase.Get("ui/stair.png")), new Material(shader, textureDatabase.Get("ui/shape_selected_stair.png"))),
            [BrickShape.Slope] = new("shapeSelector4", new Material(shader, textureDatabase.Get("ui/slope.png")), new Material(shader, textureDatabase.Get("ui/shape_selected_slope.png"))),
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
        
        windowContext.Update += OnWindowUpdate;
    }

    private void OnChangeShapePressed()
    {
        _changingShape = true;
        _previousMouseLookState = _playerControllerSystem.IsMouseLookEnabled();
        _playerControllerSystem.SetMouseLook(false);
    }
    
    private void OnChangeShapeReleased()
    {
        _changingShape = false;
        _playerControllerSystem.SetMouseLook(_previousMouseLookState);
    }

    private void OnWindowUpdate(double delta)
    {
        UIBuilder<Material> ui = _reefContext.Builder;
        
        //  TODO only draw this if the player is holding a placeable
        //  Draw the currently selected shape
        ShapeSelectorElement selectedShapeElement = _shapeSelectorElements[SelectedShape.Get()];
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
            return;
        }
        
        const float elementOffset = 72;
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
                
                bool isSelected = SelectedShape.Get() == shape;
                if (!updatedSelection && ui.Hovering())
                {
                    SelectedShape.Set(shape);
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

            using (ui.Text(SelectedShape.Get().ToString()))
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                    X = new Relative(0.5f),
                    Y = new Relative(0.5f),
                };
            }
        }
    }
}