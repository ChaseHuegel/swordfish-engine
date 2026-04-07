using System;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Extensions;
using WaywardBeyond.Client.Core.Saves;
using WaywardBeyond.Client.Core.Services;
using WaywardBeyond.Client.Core.UI.Layers.Menus.Modal;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Main;

internal sealed class SelectCharacterPage(
    in CharacterSaveManager characterSaveManager,
    in CharacterSaveService characterSaveService,
    in IInputService inputService,
    in SoundEffectService soundEffectService,
    in ILocalization localization,
    in CharacterAssetService characterAssetService,
    in GameSaveManager gameSaveManager,
    in ModalMenu modalMenu,
    in ConfirmModal confirmModal
) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.SelectCharacter;
    
    private readonly CharacterSaveManager _characterSaveManager = characterSaveManager;
    private readonly CharacterSaveService _characterSaveService = characterSaveService;
    private readonly IInputService _inputService = inputService;
    private readonly ILocalization _localization = localization;
    private readonly CharacterAssetService _characterAssetService = characterAssetService;
    private readonly GameSaveManager _gameSaveManager = gameSaveManager;
    private readonly ModalMenu _modalMenu = modalMenu;
    private readonly ConfirmModal _confirmModal = confirmModal;

    private readonly Widgets.ButtonOptions _menuButtonOptions = new(
        new FontOptions 
        {
            Size = 32,
        },
        new Widgets.AudioOptions(soundEffectService)
    );

    private readonly Widgets.ButtonOptions _buttonOptions = new(
        new FontOptions
        {
            Size = 20,
        },
        new Widgets.AudioOptions(soundEffectService)
    );
    
    private readonly Widgets.ButtonOptions _smallIconOptions = new(
        new FontOptions {
            ID = "Font Awesome 6 Free Solid",
            Size = 20,
        },
        new Widgets.AudioOptions(soundEffectService)
    );
    
    private int _scrollY;

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        using (ui.Element())
        {
            ui.Padding = new Padding(8);
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };

            using (ui.Text(_localization.GetString("ui.menu.characters")!))
            {
                ui.FontSize = 24;
            }
        }

        using (ui.Element())
        {
            ui.Spacing = 20;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
            
            if (_characterSaveManager.ActiveSave != null)
            {
                Character activeCharacter = _characterSaveManager.ActiveSave.Value.Character;
                
                Material appearanceMaterial = _characterAssetService.GetAppearanceMaterial(activeCharacter);
                using (ui.Image(appearanceMaterial))
                {
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                        Width = new Fixed(196),
                        Height = new Fixed(196),
                    };
                }
                
                using (ui.Element())
                {
                    ui.LayoutDirection = LayoutDirection.Vertical;
                    ui.Spacing = 4;
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                    };

                    using (ui.Element())
                    {
                        ui.Spacing = 8;
                        using (ui.Text(_localization.GetString("ui.label.spacer.s")!))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };
                        }
                        
                        using (ui.Text(activeCharacter.Strength.ToString()))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };

                            ui.FontSize = 20;
                            ui.Color = new Vector4(0.75f, 0.75f, 0.75f, 1f);
                        }
                    }

                    using (ui.Element())
                    {
                        ui.Spacing = 8;
                        using (ui.Text(_localization.GetString("ui.label.spacer.p")!))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };
                        }

                        using (ui.Text(activeCharacter.Precision.ToString()))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };

                            ui.FontSize = 20;
                            ui.Color = new Vector4(0.75f, 0.75f, 0.75f, 1f);
                        }
                    }

                    using (ui.Element())
                    {
                        ui.Spacing = 8;
                        using (ui.Text(_localization.GetString("ui.label.spacer.a")!))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };
                        }

                        using (ui.Text(activeCharacter.Awareness.ToString()))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };

                            ui.FontSize = 20;
                            ui.Color = new Vector4(0.75f, 0.75f, 0.75f, 1f);
                        }
                    }

                    using (ui.Element())
                    {
                        ui.Spacing = 8;
                        using (ui.Text(_localization.GetString("ui.label.spacer.c")!))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };
                        }

                        using (ui.Text(activeCharacter.Charisma.ToString()))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };

                            ui.FontSize = 20;
                            ui.Color = new Vector4(0.75f, 0.75f, 0.75f, 1f);
                        }
                    }

                    using (ui.Element())
                    {
                        ui.Spacing = 8;
                        using (ui.Text(_localization.GetString("ui.label.spacer.e")!))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };
                        }

                        using (ui.Text(activeCharacter.Education.ToString()))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };

                            ui.FontSize = 20;
                            ui.Color = new Vector4(0.75f, 0.75f, 0.75f, 1f);
                        }
                    }

                    using (ui.Element())
                    {
                        ui.Spacing = 8;
                        using (ui.Text(_localization.GetString("ui.label.spacer.r")!))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };
                        }

                        using (ui.Text(activeCharacter.Resolve.ToString()))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };

                            ui.FontSize = 20;
                            ui.Color = new Vector4(0.75f, 0.75f, 0.75f, 1f);
                        }
                    }
                }
            }

            using (ui.Element("characters"))
            {
                ui.VerticalScroll = true;

                ui.Constraints = new Constraints
                {
                    Width = new Fixed(332),
                    Height = new Fixed(196),
                };

                ui.ClipConstraints = new Constraints
                {
                    Width = new Relative(1f),
                    Height = new Relative(1f),
                };

                CharacterSave[] saves = _characterSaveService.GetSaves();
                if (saves.Length == 0)
                {
                    using (ui.Text(_localization.GetString("ui.text.none")!))
                    {
                        ui.FontOptions = _buttonOptions.FontOptions;
                        ui.Color = new Vector4(0.325f, 0.325f, 0.325f, 1f);
                        ui.Constraints = new Constraints
                        {
                            Anchors = Anchors.Center,
                        };
                    }
                }
                else
                {
                    float scroll = _inputService.GetMouseScroll();
                    _scrollY = Math.Clamp(_scrollY + (int)scroll, -(saves.Length - 1), 0);
                    ui.ScrollY = _scrollY * 30;

                    using (ui.Element())
                    {
                        ui.Spacing = 20;

                        using (ui.Element())
                        {
                            ui.LayoutDirection = LayoutDirection.Vertical;
                            ui.Spacing = 12;
                        
                            foreach (CharacterSave save in saves.OrderByDescending(save => save.Character.LastPlayedMs))
                            {
                                using (ui.TextButton(id: $"Button_DeleteCharacter_{save.Character.Guid}", text: "\uf2ed", _smallIconOptions, out Widgets.Interactions interactions))
                                {
                                    if (interactions.Has(Widgets.Interactions.Click))
                                    {
                                        _characterSaveManager.ActiveSave = save;
                                        _modalMenu.GoToPage(_confirmModal.Create(DeleteSave));

                                        void DeleteSave()
                                        {
                                            _characterSaveManager.Delete(save);
                                            _characterSaveManager.ActiveSave = _characterSaveManager.GetMostRecentSave();
                                        }
                                    }
                                }
                            }
                        }
                        
                        using (ui.Element())
                        {
                            ui.LayoutDirection = LayoutDirection.Vertical;

                            foreach (CharacterSave save in saves.OrderByDescending(save => save.Character.LastPlayedMs))
                            {
                                using (ui.TextButton(id: $"Button_SelectCharacter_{save.Character.Guid}", text: save.Character.Name, _buttonOptions, out Widgets.Interactions interactions))
                                {
                                    ui.Constraints = new Constraints
                                    {
                                        Anchors = Anchors.Center | Anchors.Left,
                                    };

                                    if (_characterSaveManager.ActiveSave != null &&
                                        _characterSaveManager.ActiveSave.Value.Character.Guid == save.Character.Guid)
                                    {
                                        ui.Color = new Vector4(0f, 0.455f, 1f, 0.5f);
                                    }

                                    if (interactions.Has(Widgets.Interactions.Click))
                                    {
                                        _characterSaveManager.ActiveSave = save;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        
        if (_characterSaveManager.ActiveSave != null)
        {
            Character activeCharacter = _characterSaveManager.ActiveSave.Value.Character;
            
            using (ui.Element())
            {
                ui.Spacing = 8;
                ui.Padding = new Padding(16);
                ui.LayoutDirection = LayoutDirection.Vertical;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };
                
                using (ui.Element())
                {
                    ui.Spacing = 8;
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                    };

                    DateTimeOffset lastPlayed = DateTimeOffset.FromUnixTimeMilliseconds(activeCharacter.LastPlayedMs);
                    using (ui.Text(_localization.GetString("ui.label.lastPlayed")!)) { }
                    using (ui.Text(lastPlayed.ToLocalTime().ToString(format: "g")))
                    {
                        ui.Color = new Vector4(0.75f, 0.75f, 0.75f, 1f);
                    }
                }

                TimeSpan age = TimeSpan.FromMilliseconds(activeCharacter.AgeMs);
                string timePlayedStr = _localization.GetLongString(age);
                using (ui.Element())
                {
                    ui.Spacing = 8;
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                    };
                    
                    using (ui.Text(_localization.GetString("ui.label.timePlayed")!)) { }
                    using (ui.Text(timePlayedStr))
                    {
                        ui.Color = new Vector4(0.75f, 0.75f, 0.75f, 1f);
                    }
                }
            }
            
            using (ui.TextButton(id: "Button_LoadSave", text: _localization.GetString("ui.button.loadSave")!, _menuButtonOptions, out Widgets.Interactions interactions))
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };
            
                if (interactions.Has(Widgets.Interactions.Click))
                {
                    Task.Run(_gameSaveManager.Load);
                }
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