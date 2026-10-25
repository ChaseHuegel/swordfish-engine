using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Numerics;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Characters;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class NewCharacterPage : IMenuPage<MenuPage>
{
    private const int DEFAULT_SPACER_POINTS = 18;
    
    private static readonly char[] _characterNameTrimChars = [' ', '\t', '.', '\n', '\r'];

    public MenuPage ID => MenuPage.NewCharacter;

    private readonly ILogger<NewCharacterPage> _logger;
    private readonly ICharacterStorage _characterStorage;
    private readonly CharacterSaveManager _characterSaveManager;
    private readonly IInputService _inputService;
    private readonly SoundEffectService _soundEffectService;
    private readonly ILocalization _localization;
    private readonly CharacterAssetService _characterAssetService;
    private readonly NameGenerator _nameGenerator;
    private readonly NotificationService _notificationService;

    private readonly Widgets.ActionButtonOptions _primaryButton;
    private readonly Widgets.ActionButtonOptions _subtleButton;
    private readonly Widgets.ActionButtonOptions _iconButton;
    private readonly Widgets.ActionButtonOptions _smallIconButton;
    private readonly FontOptions _bodyFont;
    private readonly Randomizer _randomizer = new();

    private int _characterMaterialIndex;
    private TextBoxState _nameTextBox;

    private int _spacerPoints = DEFAULT_SPACER_POINTS;
    private int _strength = 1;
    private int _precision = 1;
    private int _awareness = 1;
    private int _charisma = 1;
    private int _education = 1;
    private int _resolve = 1;

    public NewCharacterPage(
        in ILogger<NewCharacterPage> logger,
        in ICharacterStorage characterStorage,
        in CharacterSaveManager characterSaveManager,
        in IInputService inputService,
        in SoundEffectService soundEffectService,
        in ILocalization localization,
        in CharacterAssetService characterAssetService,
        in NameGenerator nameGenerator,
        in NotificationService notificationService
    ) {
        _logger = logger;
        _characterStorage = characterStorage;
        _characterSaveManager = characterSaveManager;
        _inputService = inputService;
        _soundEffectService = soundEffectService;
        _localization = localization;
        _characterAssetService = characterAssetService;
        _nameGenerator = nameGenerator;
        _notificationService = notificationService;

        _primaryButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Primary, soundEffectService) with
        {
            Anchors = Anchors.Center,
        };
        _subtleButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Subtle, soundEffectService) with
        {
            Anchors = Anchors.Center,
        };
        _iconButton = Widgets.ActionButtonOptions.Icon(
            ButtonVariant.Secondary,
            soundEffectService,
            size: UiMetrics.IconButtonSize,
            fontSize: UiMetrics.LargeIconFont,
            fontId: "Font Awesome 6 Free Solid"
        );
        _smallIconButton = Widgets.ActionButtonOptions.Icon(
            ButtonVariant.Secondary,
            soundEffectService,
            size: UiMetrics.SmallIconButtonSize,
            fontSize: UiMetrics.IconFont,
            fontId: "Font Awesome 6 Free Solid"
        );
        _bodyFont = new FontOptions
        {
            Size = UiMetrics.BodyFont,
        };

        var saveNameTextBoxOptions = new TextBoxState.Options(
            Placeholder: localization.GetString("ui.field.characterName"),
            MaxCharacters: 20,
            DisallowedCharacters: ['\0', '\\', '/', ':', '*', '?', '<', '>', '|'],
            Constraints: new Constraints
            {
                Width = new Fixed(300),
            }
        );
        _nameTextBox = new TextBoxState(initialValue: string.Empty, options: saveNameTextBoxOptions);
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        bool randomAll = false;
        using (ui.Element())
        {
            ui.Spacing = 8;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
            
            using (ui.Text(_localization.GetString("ui.menu.createCharacter")!))
            {
                ui.FontSize = UiMetrics.TitleFont;
            }
            
            if (ui.ActionButton("Button_RandomAll", "\uf074", _smallIconButton))
            {
                randomAll = true;
            }
        }

        var validName = true;
        string nameValue;
        
        using (ui.Element())
        {
            ui.Spacing = 32;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Height = new Fixed(400),
            };
            
            using (ui.Element())
            {
                ui.Spacing = 20;
                ui.LayoutDirection = LayoutDirection.Vertical;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };
                
                if (_characterAssetService.GetAppearancesCount() > 0)
                {
                    using (ui.Element())
                    {
                        ui.LayoutDirection = LayoutDirection.Vertical;
                        ui.Spacing = 20;
                        ui.Padding = new Padding(left: 0, top: 20, right: 0, bottom: 20);
                        ui.Constraints = new Constraints
                        {
                            Anchors = Anchors.Center,
                        };
                        
                        bool randomizeCharacter = randomAll;
                        if (ui.ActionButton("Button_RandomCharacter", "\uf074", _iconButton))
                        {
                            randomizeCharacter = true;
                        }

                        if (randomizeCharacter)
                        {
                            _characterMaterialIndex = _randomizer.NextInt(0, _characterAssetService.GetAppearancesCount());
                        }

                        using (ui.Element())
                        {
                            ui.LayoutDirection = LayoutDirection.Horizontal;
                            ui.Spacing = 12;
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };

                            if (ui.ActionButton("Button_PreviousCharacter", "\uf0d9", _iconButton))
                            {
                                _characterMaterialIndex = MathS.WrapInt(_characterMaterialIndex - 1, 0, _characterAssetService.GetAppearancesCount() - 1);
                            }

                            Material appearanceMaterial = _characterAssetService.GetAppearanceMaterial(_characterAssetService.GetBodyId(_characterMaterialIndex));
                            using (ui.Image(appearanceMaterial))
                            {
                                ui.Constraints = new Constraints
                                {
                                    Width = new Fixed(196),
                                    Height = new Fixed(196),
                                };
                            }

                            if (ui.ActionButton("Button_NextCharacter", "\uf0da", _iconButton))
                            {
                                _characterMaterialIndex = MathS.WrapInt(_characterMaterialIndex + 1, 0, _characterAssetService.GetAppearancesCount() - 1);
                            }
                        }
                    }
                }

                using (ui.Element())
                {
                    ui.Spacing = 8;
                    
                    ui.TextBox(id: "TextBox_CharacterName", state: ref _nameTextBox, _bodyFont, _inputService, _soundEffectService);

                    bool randomizeName = randomAll;
                    if (ui.ActionButton("Button_RandomName", "\uf074", _smallIconButton))
                    {
                        randomizeName = true;
                    }

                    if (randomizeName)
                    {
                        NameGenerator.Options nameGeneratorOptions = new NameGenerator.Options(
                            TitleChance: 0.2f,
                            FirstNameChance: 0.7f,
                            LastNameChance: 1.0f,
                            SubtitleChance: 0.1f,
                            NicknameChance: 0.1f
                        );

                        string generatedName = _nameGenerator.Generate(key: _characterAssetService.GetBodyId(_characterMaterialIndex), nameGeneratorOptions);
                        _nameTextBox.Text.Clear();
                        _nameTextBox.Text.Append(generatedName);
                    }
                }

                nameValue = CharacterNames.Clamp(_nameTextBox.Text.ToString().Trim(_characterNameTrimChars));
                if (string.IsNullOrWhiteSpace(nameValue))
                {
                    using (ui.Text(_localization.GetString("ui.notification.name.required")!))
                    {
                        ui.Color = UiTheme.Danger;
                    }

                    validName = false;
                }
                else if (_characterStorage.GetAllCharacters().Any(character => character.Name == nameValue))
                {
                    using (ui.Text(_localization.GetString("ui.notification.name.taken")!))
                    {
                        ui.Color = UiTheme.Danger;
                    }

                    validName = false;
                }
            }

            using (ui.Element())
            {
                ui.LayoutDirection = LayoutDirection.Vertical;
                
                ui.Constraints = new Constraints
                {
                    Width = new Fixed(300),
                    Height = new Relative(1f),
                };
                
                using (ui.Element())
                {
                    ui.LayoutDirection = LayoutDirection.Vertical;
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                    };

                    using (ui.Text(_localization.GetString("ui.field.spacer")!))
                    {
                        ui.FontSize = UiMetrics.TitleFont;
                    }
                }

                using (ui.Text(_localization.GetString("ui.field.spacer.description")!))
                {
                    ui.Color = UiTheme.TextSoft;
                }

                using (ui.Element())
                {
                    ui.Constraints = new Constraints
                    {
                        Width = new Relative(1f),
                        Height = new Fixed(20),
                    };
                }

                using (ui.Element())
                {
                    ui.Spacing = 8;
                    ui.Constraints = new Constraints
                    {
                        Width = new Relative(1f),
                    };
                    
                    using (ui.Text(_localization.GetString("ui.field.spacer.points")!)) { }
                    
                    using (ui.Element())
                    {
                        ui.Constraints = new Constraints
                        {
                            Width = new Fill(),
                            Height = new Fill(),
                        };
                    }
                    
                    using (ui.Text($"{_spacerPoints}"))
                    {
                        ui.FontSize = UiMetrics.BodyFont;
                    }
                    
                    bool randomizePoints = randomAll;
                    if (ui.ActionButton("Button_RandomPoints", "\uf074", _smallIconButton))
                    {
                        randomizePoints = true;
                    }

                    if (randomizePoints)
                    {
                        //  If there are no points available, randomize all attributes.
                        //  Otherwise, whatever points remain will be randomly distributed.
                        if (_spacerPoints == 0)
                        {
                            ResetAttributes();
                        }

                        while (_spacerPoints > 0)
                        {
                            int attributeIndex = _randomizer.NextInt(0, 6);
                            switch (attributeIndex)
                            {
                                case 0:
                                    OnStrengthChanged(_strength, _strength + 1, change: 1);
                                    break;
                                case 1:
                                    OnPrecisionChanged(_precision, _precision + 1, change: 1);
                                    break;
                                case 2:
                                    OnAwarenessChanged(_awareness, _awareness + 1, change: 1);
                                    break;
                                case 3:
                                    OnCharismaChanged(_charisma, _charisma + 1, change: 1);
                                    break;
                                case 4:
                                    OnEducationChanged(_education, _education + 1, change: 1);
                                    break;
                                case 5:
                                    OnResolveChanged(_resolve, _resolve + 1, change: 1);
                                    break;
                            }
                        }
                    }
                }

                ui.NumberControl(
                    id: "Control_Strength",
                    text: _localization.GetString("ui.field.strength")!,
                    _strength,
                    constraints: new Int2(1, 8),
                    display: new Int2(1, 8),
                    steps: 7,
                    _soundEffectService,
                    OnStrengthChanged
                );

                ui.NumberControl(
                    id: "Control_Precision",
                    text: _localization.GetString("ui.field.precision")!,
                    _precision,
                    constraints: new Int2(1, 8),
                    display: new Int2(1, 8),
                    steps: 7,
                    _soundEffectService,
                    OnPrecisionChanged
                );

                ui.NumberControl(
                    id: "Control_Awareness",
                    text: _localization.GetString("ui.field.awareness")!,
                    _awareness,
                    constraints: new Int2(1, 8),
                    display: new Int2(1, 8),
                    steps: 7,
                    _soundEffectService,
                    OnAwarenessChanged
                );

                ui.NumberControl(
                    id: "Control_Charisma",
                    text: _localization.GetString("ui.field.charisma")!,
                    _charisma,
                    constraints: new Int2(1, 8),
                    display: new Int2(1, 8),
                    steps: 7,
                    _soundEffectService,
                    OnCharismaChanged
                );

                ui.NumberControl(
                    id: "Control_Education",
                    text: _localization.GetString("ui.field.education")!,
                    _education,
                    constraints: new Int2(1, 8),
                    display: new Int2(1, 8),
                    steps: 7,
                    _soundEffectService,
                    OnEducationChanged
                );

                ui.NumberControl(
                    id: "Control_Resolve",
                    text: _localization.GetString("ui.field.resolve")!,
                    _resolve,
                    constraints: new Int2(1, 8),
                    display: new Int2(1, 8),
                    steps: 7,
                    _soundEffectService,
                    OnResolveChanged
                );

                void OnStrengthChanged(int oldValue, int newValue, int change)
                {
                    if (change > 0 && _spacerPoints - change < 0)
                    {
                        return;
                    }

                    _strength = newValue;
                    _spacerPoints -= change;
                }

                void OnPrecisionChanged(int oldValue, int newValue, int change)
                {
                    if (change > 0 && _spacerPoints - change < 0)
                    {
                        return;
                    }

                    _precision = newValue;
                    _spacerPoints -= change;
                }

                void OnAwarenessChanged(int oldValue, int newValue, int change)
                {
                    if (change > 0 && _spacerPoints - change < 0)
                    {
                        return;
                    }

                    _awareness = newValue;
                    _spacerPoints -= change;
                }

                void OnCharismaChanged(int oldValue, int newValue, int change)
                {
                    if (change > 0 && _spacerPoints - change < 0)
                    {
                        return;
                    }

                    _charisma = newValue;
                    _spacerPoints -= change;
                }

                void OnEducationChanged(int oldValue, int newValue, int change)
                {
                    if (change > 0 && _spacerPoints - change < 0)
                    {
                        return;
                    }

                    _education = newValue;
                    _spacerPoints -= change;
                }

                void OnResolveChanged(int oldValue, int newValue, int change)
                {
                    if (change > 0 && _spacerPoints - change < 0)
                    {
                        return;
                    }

                    _resolve = newValue;
                    _spacerPoints -= change;
                }
            }
        }

        if (validName && ui.ActionButton("Button_CreateCharacter", _localization.GetString("ui.button.createCharacter")!, _primaryButton))
        {
            var character = new Character(
                WaywardBeyond.Version,
                Uuid.NewUuid().ToValue(),
                _Playtime: new Playtime(),
                nameValue,
                _strength,
                _precision,
                _awareness,
                _charisma,
                _education,
                _resolve,
                _Body: _characterAssetService.GetBodyId(_characterMaterialIndex),
                _ActiveInventorySlot: 0,
                _GameMode: GameMode.Creative,
                _Statistics: null,
                _Inventory: null
            );

            Task.Run(() =>
                {
                    Result saveResult = _characterStorage.SaveCharacter(character);
                    if (saveResult)
                    {
                        _characterSaveManager.ActiveSave = character;
                        menu.GoToPage(MenuPage.SelectCharacter);
                        ResetState();
                    }
                    else
                    {
                        var notification = new Notification("Failed to create character!");
                        _notificationService.Push(notification);

                        _logger.LogError(saveResult.Exception, "Failed to save character \"{name}\" ({id}): {message}", character.Name, character.Uuid, saveResult.Message);
                    }
                }
            );
        }
        
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            };
        }
        
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
            
            if (ui.ActionButton("Button_Back", _localization.GetString("ui.button.back")!, _subtleButton))
            {
                menu.GoBack();
            }
        }
        
        return Result.FromSuccess();
    }
    
    private void ResetState()
    {
        _characterMaterialIndex = 0;
        _nameTextBox.Text.Clear();
        ResetAttributes();
    }

    private void ResetAttributes()
    {
        _spacerPoints = DEFAULT_SPACER_POINTS;
        _strength = 1;
        _precision = 1;
        _awareness = 1;
        _charisma = 1;
        _education = 1;
        _resolve = 1;
    }
}