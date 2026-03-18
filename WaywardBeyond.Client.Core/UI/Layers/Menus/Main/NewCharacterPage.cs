using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Client.Core.Saves;
using WaywardBeyond.Client.Core.Services;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Main;

internal sealed class NewCharacterPage : IMenuPage<MenuPage>
{
    private static readonly char[] _characterNameTrimChars = [' ', '\t', '.', '\n', '\r'];

    public MenuPage ID => MenuPage.NewCharacter;
    
    private readonly CharacterSaveService _characterSaveService;
    private readonly IInputService _inputService;
    private readonly SoundEffectService _soundEffectService;
    private readonly ILocalization _localization;

    private readonly Widgets.ButtonOptions _menuButtonOptions;
    private readonly Widgets.ButtonOptions _buttonOptions;
    private readonly Widgets.ButtonOptions _iconOptions;

    private readonly List<Material> _characterMaterials;

    private int _characterMaterialIndex;
    private TextBoxState _nameTextBox;

    private int _spacerPoints = 18;
    private int _strength = 1;
    private int _precision = 1;
    private int _awareness = 1;
    private int _charisma = 1;
    private int _education = 1;
    private int _resolve = 1;

    public NewCharacterPage(
        in CharacterSaveService characterSaveService,
        in IInputService inputService,
        in SoundEffectService soundEffectService,
        in ILocalization localization,
        in IAssetDatabase<Material> materialDatabase,
        in VirtualFileSystem vfs
    ) {
        _characterSaveService = characterSaveService;
        _inputService = inputService;
        _soundEffectService = soundEffectService;
        _localization = localization;
        
        PathInfo characterMaterialsPath = AssetPaths.Materials.At("characters/");
        IEnumerable<string> characterMaterialIds = vfs.GetFiles(characterMaterialsPath, SearchOption.TopDirectoryOnly)
            .Select(pathInfo => $"characters/{pathInfo.GetFileNameWithoutExtension()}");

        _characterMaterials = [];
        foreach (string id in characterMaterialIds)
        {
            Result<Material> materialResult = materialDatabase.Get(id);
            if (materialResult)
            {
                _characterMaterials.Add(materialResult);
            }
        }

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
                Size = 48,
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
        var nameValue = string.Empty;
        
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
                    Anchors = Anchors.Center | Anchors.Top,
                };
                
                if (_characterMaterials.Count > 0)
                {
                    using (ui.Element())
                    {
                        ui.Padding = new Padding(left: 0, top: 20, right: 0, bottom: 20);
                        ui.Constraints = new Constraints
                        {
                            Anchors = Anchors.Center,
                        };

                        using (ui.TextButton(id: "Button_PreviousCharacter", text: "\uf0d9", _iconOptions,
                                   out Widgets.Interactions interactions))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };

                            if (interactions.Has(Widgets.Interactions.Click))
                            {
                                _characterMaterialIndex = MathS.WrapInt(_characterMaterialIndex - 1, 0,
                                    _characterMaterials.Count - 1);
                            }
                        }

                        using (ui.Image(_characterMaterials[_characterMaterialIndex]))
                        {
                            ui.Constraints = new Constraints
                            {
                                Width = new Fixed(256),
                                Height = new Fixed(256),
                            };
                        }

                        using (ui.TextButton(id: "Button_NextCharacter", text: "\uf0da", _iconOptions,
                                   out Widgets.Interactions interactions))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };

                            if (interactions.Has(Widgets.Interactions.Click))
                            {
                                _characterMaterialIndex = MathS.WrapInt(_characterMaterialIndex + 1, 0,
                                    _characterMaterials.Count - 1);
                            }
                        }
                    }
                }

                ui.TextBox(id: "TextBox_CharacterName", state: ref _nameTextBox, _buttonOptions.FontOptions,
                    _inputService, _soundEffectService);

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
                    
                    using (ui.Element())
                    {
                        ui.Constraints = new Constraints
                        {
                            Width = new Fixed(24),
                        };
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
                var character = new Character(WaywardBeyond.Version, Guid.NewGuid().ToString(), _LastPlayedMs: 0, _AgeMs: 0, nameValue);
                Task.Run(() => _characterSaveService.CreateSave(character));
                menu.GoBack();
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
}