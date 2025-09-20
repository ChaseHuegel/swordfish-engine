using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Shoal.DependencyInjection;
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

using ShapeSelectorElement = (string ID, Material BaseImage, Material SelectedImage);

internal class BrickSelector : IAutoActivate
{
    public readonly DataBinding<BrickShape> SelectedShape = new(BrickShape.Block);
    
    private readonly ILogger _logger;
    private readonly ReefContext _reefContext;
    private readonly PlayerControllerSystem _playerControllerSystem;
    private readonly PlayerData _playerData;
    private readonly BrickDatabase _brickDatabase;
    private readonly IInputService _inputService;
    
    private readonly Material _labelImage;
    private readonly Material _backgroundImage;
    private readonly Dictionary<BrickShape, ShapeSelectorElement> _shapeSelectorElements;
    private readonly List<BrickInfo> _buildableBricks;
    
    private bool _changingBrick;
    
    public BrickSelector(
        ILogger<BrickSelector> logger,
        IWindowContext windowContext,
        ReefContext reefContext,
        IShortcutService shortcutService,
        IAssetDatabase<Texture> textureDatabase,
        IAssetDatabase<Shader> shaderDatabase,
        PlayerControllerSystem playerControllerSystem,
        PlayerData playerData,
        BrickDatabase brickDatabase,
        IInputService inputService
    ) {
        _logger = logger;
        _reefContext = reefContext;
        _playerControllerSystem = playerControllerSystem;
        _playerData = playerData;
        _brickDatabase = brickDatabase;
        _inputService = inputService;
        
        Result<Shader> shader = shaderDatabase.Get("ui_reef_textured.glsl");
        _backgroundImage = new Material(shader, textureDatabase.Get("ui/shape_background.png"));
        _labelImage = new Material(shader, textureDatabase.Get("ui/shape_label.png"));
        
        _shapeSelectorElements = new Dictionary<BrickShape, ShapeSelectorElement>
        {
            [BrickShape.Block] = new("brickSelector1", new Material(shader, textureDatabase.Get("ui/block.png")), new Material(shader, textureDatabase.Get("ui/shape_selected_block.png"))),
            [BrickShape.Slab]  = new("brickSelector2", new Material(shader, textureDatabase.Get("ui/slab.png")), new Material(shader, textureDatabase.Get("ui/shape_selected_slab.png"))),
            [BrickShape.Stair] = new("brickSelector3", new Material(shader, textureDatabase.Get("ui/stair.png")), new Material(shader, textureDatabase.Get("ui/shape_selected_stair.png"))),
            [BrickShape.Slope] = new("brickSelector4", new Material(shader, textureDatabase.Get("ui/slope.png")), new Material(shader, textureDatabase.Get("ui/shape_selected_slope.png"))),
        };

        _buildableBricks = brickDatabase.Get(brickInfo => brickInfo.Tags.Contains("buildable"));

        var shortcut = new Shortcut
        {
            Name = "Change brick",
            Category = "Interaction",
            Modifiers = ShortcutModifiers.None,
            Key = Key.G,
            IsEnabled = Shortcut.DefaultEnabled,
            Action = OnChangeBrickPressed,
            Released = OnChangeBrickReleased,
        };
        
        shortcutService.RegisterShortcut(shortcut);
        
        windowContext.Update += OnWindowUpdate;
    }

    private void OnChangeBrickPressed()
    {
        if (!IsMainHandShapeable())
        {
            return;
        }
        
        _changingBrick = true;
    }
    
    private void OnChangeBrickReleased()
    {
        if (!IsMainHandShapeable())
        {
            return;
        }
        
        _changingBrick = false;
    }
    
    private bool IsMainHandShapeable() 
    {
        Result<ItemSlot> mainHandResult = _playerData.GetMainHand();
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

        return brickInfoResult.Value.Shape == BrickShape.Any;
    }

    private int _scrollY;
    private void OnWindowUpdate(double delta)
    {
        if (!IsMainHandShapeable())
        {
            //  Don't draw anything if the player isn't holding a shapeable item.
            return;
        }
        
        //  Only display the selector if changing bricks
        if (!_changingBrick)
        {
            return;
        }

        UIBuilder<Material> ui = _reefContext.Builder;
        
        using (ui.Element())
        {
            ui.Color = new Vector4(0.25f, 0.25f, 0.25f, 1f);
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Spacing = 8;
            ui.Padding = new Padding(
                left: 8,
                top: 8,
                right: 8,
                bottom: 8
            );
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Left,
                X = new Relative(0.1f),
                Y = new Relative(0.5f),
                Height = new Relative(0.5f),
            };
            ui.VerticalScroll = true;

            _scrollY -= (int)(_inputService.GetMouseScroll() * 64f);
            ui.ScrollY = _scrollY;
        
            for (int i = 0; i < _buildableBricks.Count; i++)
            {
                BrickInfo brickInfo = _buildableBricks[i];
                using (ui.Element())
                {
                    ui.Padding = new Padding(left: 4, top: 4, right: 4, bottom: 4);
                    ui.Color = new Vector4(0f, 0.5f, 0.5f, 1f);
                    ui.Constraints = new Constraints
                    {
                        Width = new Fixed(48),
                        Height = new Fixed(48),
                    };
                    
                    using (ui.Text(brickInfo.ID.Replace('_', ' ')))
                    {
                        ui.FontSize = 12;
                    }
                }
            }
        }
    }
}