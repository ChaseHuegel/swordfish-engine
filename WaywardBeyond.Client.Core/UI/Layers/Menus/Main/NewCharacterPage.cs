using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Meta;
using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Client.Core.Saves;
using WaywardBeyond.Client.Core.Services;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Main;

internal sealed class NewCharacterPage : IMenuPage<MenuPage>
{
    private const int DEFAULT_SPACER_POINTS = 18;
    
    private static readonly char[] _characterNameTrimChars = [' ', '\t', '.', '\n', '\r'];

    public MenuPage ID => MenuPage.NewCharacter;
    
    private readonly CharacterSaveService _characterSaveService;
    private readonly CharacterSaveManager _characterSaveManager;
    private readonly IInputService _inputService;
    private readonly SoundEffectService _soundEffectService;
    private readonly ILocalization _localization;
    private readonly CharacterAssetService _characterAssetService;
    private readonly NameGenerator _nameGenerator;

    private readonly Widgets.ButtonOptions _menuButtonOptions;
    private readonly Widgets.ButtonOptions _buttonOptions;
    private readonly Widgets.ButtonOptions _iconOptions;
    private readonly Widgets.ButtonOptions _smallIconOptions;
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
        in CharacterSaveService characterSaveService,
        in CharacterSaveManager characterSaveManager,
        in IInputService inputService,
        in SoundEffectService soundEffectService,
        in ILocalization localization,
        in CharacterAssetService characterAssetService,
        in NameGenerator nameGenerator
    ) {
        _characterSaveService = characterSaveService;
        _characterSaveManager = characterSaveManager;
        _inputService = inputService;
        _soundEffectService = soundEffectService;
        _localization = localization;
        _characterAssetService = characterAssetService;
        _nameGenerator = nameGenerator;

        _menuButtonOptions = new Widgets.ButtonOptions(
            new FontOptions {
                Size = 32,
            },
            new Widgets.AudioOptions(soundEffectService)
        );
        
        _buttonOptions = new Widgets.ButtonOptions(
            new FontOptions {
                Size = 20,
            },
            new Widgets.AudioOptions(soundEffectService)
        );
        
        _iconOptions = new Widgets.ButtonOptions(
            new FontOptions {
                ID = "Font Awesome 6 Free Solid",
                Size = 32,
            },
            new Widgets.AudioOptions(soundEffectService)
        );
        
        _smallIconOptions = new Widgets.ButtonOptions(
            new FontOptions {
                ID = "Font Awesome 6 Free Solid",
                Size = 20,
            },
            new Widgets.AudioOptions(soundEffectService)
        );

        var saveNameTextBoxOptions = new TextBoxState.Options(
            Placeholder: localization.GetString("ui.field.characterName"),
            MaxCharacters: 20,
            DisallowedCharacters: ['\0', '\\', '/', ':', '*', '?', '"', '<', '>', '|'],
            Constraints: new Constraints
            {
                Width = new Fixed(300),
            }
        );
        _nameTextBox = new TextBoxState(initialValue: string.Empty, options: saveNameTextBoxOptions);
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
            
            using (ui.Text(_localization.GetString("ui.menu.createCharacter")!))
            {
                ui.FontSize = 24;
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
                        
                        using (ui.TextButton(id: "Button_RandomCharacter", text: "\uf074", _iconOptions, out Widgets.Interactions interactions))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };

                            if (interactions.Has(Widgets.Interactions.Click))
                            {
                                _characterMaterialIndex = _randomizer.NextInt(0, _characterAssetService.GetAppearancesCount());
                            }
                        }

                        using (ui.Element())
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };
                            
                            using (ui.TextButton(id: "Button_PreviousCharacter", text: "\uf0d9", _iconOptions, out Widgets.Interactions interactions))
                            {
                                ui.Constraints = new Constraints
                                {
                                    Anchors = Anchors.Center,
                                };

                                if (interactions.Has(Widgets.Interactions.Click))
                                {
                                    _characterMaterialIndex = MathS.WrapInt(_characterMaterialIndex - 1, 0, _characterAssetService.GetAppearancesCount() - 1);
                                }
                            }

                            Material appearanceMaterial = _characterAssetService.GetAppearanceMaterial(_characterMaterialIndex);
                            using (ui.Image(appearanceMaterial))
                            {
                                ui.Constraints = new Constraints
                                {
                                    Width = new Fixed(196),
                                    Height = new Fixed(196),
                                };
                            }

                            using (ui.TextButton(id: "Button_NextCharacter", text: "\uf0da", _iconOptions, out Widgets.Interactions interactions))
                            {
                                ui.Constraints = new Constraints
                                {
                                    Anchors = Anchors.Center,
                                };

                                if (interactions.Has(Widgets.Interactions.Click))
                                {
                                    _characterMaterialIndex = MathS.WrapInt(_characterMaterialIndex + 1, 0, _characterAssetService.GetAppearancesCount() - 1);
                                }
                            }
                        }
                    }
                }

                using (ui.Element())
                {
                    ui.Spacing = 8;
                    
                    ui.TextBox(id: "TextBox_CharacterName", state: ref _nameTextBox, _buttonOptions.FontOptions, _inputService, _soundEffectService);
                    
                    using (ui.TextButton(id: "Button_RandomName", text: "\uf074", _smallIconOptions, out Widgets.Interactions interactions))
                    {
                        if (interactions.Has(Widgets.Interactions.Click))
                        {
                            string generatedName = _nameGenerator.Generate(key: _characterMaterialIndex.ToString());
                            _nameTextBox.Text.Clear();
                            _nameTextBox.Text.Append(generatedName);
                        }
                    }
                }

                nameValue = _nameTextBox.Text.ToString().Trim(_characterNameTrimChars);
                if (string.IsNullOrWhiteSpace(nameValue))
                {
                    using (ui.Text(_localization.GetString("ui.notification.name.required")!))
                    {
                        ui.Color = new Vector4(1f, 0f, 0f, 1f);
                    }

                    validName = false;
                }
                else if (_characterSaveService.GetSaves().Any(save => save.Character.Name == nameValue))
                {
                    using (ui.Text(_localization.GetString("ui.notification.name.taken")!))
                    {
                        ui.Color = new Vector4(1f, 0f, 0f, 1f);
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
                        ui.FontSize = 24;
                    }
                }

                using (ui.Text(_localization.GetString("ui.field.spacer.description")!))
                {
                    ui.Color = new Vector4(0.75f, 0.75f, 0.75f, 1f);
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
                        ui.FontSize = 20;
                    }
                    
                    using (ui.TextButton(id: "Button_RandomPoints", text: "\uf074", _smallIconOptions, out Widgets.Interactions interactions))
                    {
                        if (interactions.Has(Widgets.Interactions.Click))
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

        using (ui.TextButton(id: "Button_CreateCharacter", text: _localization.GetString("ui.button.createCharacter")!, _menuButtonOptions, out Widgets.Interactions interactions))
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
            
            if (validName && interactions.Has(Widgets.Interactions.Click))
            {
                var character = new Character(
                    WaywardBeyond.Version,
                    Guid.NewGuid().ToString(),
                    _LastPlayedMs: 0,
                    _AgeMs: 0,
                    nameValue,
                    _strength,
                    _precision,
                    _awareness,
                    _charisma,
                    _education,
                    _resolve,
                    _Body: _characterMaterialIndex,
                     _Inventory: null
                );
                
                Task.Run(() =>
                    {
                        Result<CharacterSave> save = _characterSaveService.CreateSave(character);
                        if (save.Success)
                        {
                            _characterSaveManager.ActiveSave = save.Value;
                        }
                    }
                );

                menu.GoToPage(MenuPage.SelectCharacter);
                ResetState();
            }
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
            
            using (ui.TextButton(id: "Button_Back", text: _localization.GetString("ui.button.back")!, _menuButtonOptions, out Widgets.Interactions interactions))
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };

                if (interactions.Has(Widgets.Interactions.Click))
                {
                    menu.GoBack();
                }
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